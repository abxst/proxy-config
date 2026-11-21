#!/usr/bin/env python3
"""Inventory officially documented nginx and Apache HTTP Server directives.

This is a documentation snapshot, NOT a complete syntax validator. Availability,
version constraints, directive-specific semantics, and generated config behavior
must be curated/tested before being enabled by a GUI generator.
"""

from __future__ import annotations

import concurrent.futures
import datetime as dt
import json
import re
import subprocess
import sys
import urllib.parse
from collections import Counter, defaultdict
from pathlib import Path

from lxml import html


ROOT = Path(__file__).resolve().parent
NG = "https://nginx.org/en/docs/"
AP = "https://httpd.apache.org/docs/2.4/"


def tidy(text: str) -> str:
    return re.sub(r"\s+", " ", text or "").strip()


def fetch(url: str) -> tuple[str, bytes, str | None]:
    for attempt in range(2):
        proc = subprocess.run(
            ["curl", "-fsSL", "--retry", "1", "--connect-timeout", "10", "--max-time", "32", url],
            capture_output=True,
            timeout=38,
        )
        if proc.returncode == 0 and proc.stdout:
            return url, proc.stdout, None
    return url, b"", tidy(proc.stderr.decode("utf-8", errors="replace"))[-300:]


def page(url: str) -> html.HtmlElement:
    _, data, error = fetch(url)
    if error:
        raise RuntimeError(f"{url}: {error}")
    return html.fromstring(data)


def index_entries(tree: html.HtmlElement, root: str) -> list[dict]:
    out = []
    for a in tree.xpath('//a[contains(@href,"#")]'):
        href = a.get("href", "")
        if href.startswith("#") or not a.text_content().strip():
            continue
        absolute = urllib.parse.urljoin(root, href)
        url, anchor = urllib.parse.urldefrag(absolute)
        if not anchor:
            continue
        out.append({
            "name": tidy(a.text_content()),
            "url": absolute,
            "module_url": url,
            "anchor": anchor,
            "module": url.split("/")[-1].removesuffix(".html"),
        })
    return out


def module_urls(tree: html.HtmlElement, base: str, apache: bool) -> set[str]:
    urls = set()
    for a in tree.xpath("//a[@href]"):
        href = a.get("href", "")
        url = urllib.parse.urldefrag(urllib.parse.urljoin(base, href))[0]
        name = urllib.parse.urlparse(url).path.rsplit("/", 1)[-1]
        label = tidy(a.text_content())
        if apache:
            matches = (
                url.startswith(AP + "mod/")
                and name.endswith(".html")
                and (name.startswith("mod_") or name in {"core.html", "mpm_common.html", "event.html", "worker.html", "prefork.html", "mpm_winnt.html", "mpm_netware.html", "mpmt_os2.html"})
            )
        else:
            matches = url.startswith(NG) and name.endswith(".html") and (
                name.startswith("ngx_") or name == "ngx_core_module.html"
            )
        if matches and label:
            urls.add(url)
    return urls


def table_fields(table: html.HtmlElement) -> dict:
    fields = {}
    for tr in table.xpath("./tr|./tbody/tr"):
        heads = tr.xpath("./th")
        cells = tr.xpath("./td")
        if heads and cells:
            key = tidy(heads[0].text_content()).rstrip(":").lower()
            fields[key] = tidy(cells[0].text_content())
    return fields


def extract_nginx(tree: html.HtmlElement, entries: list[dict]) -> list[dict]:
    items = []
    for e in entries:
        nodes = tree.xpath('//a[@name=$anchor] | //*[@id=$anchor]', anchor=e["anchor"])
        fields = {}
        if nodes:
            for sibling in nodes[0].itersiblings():
                if sibling.tag == "div" and "directive" in sibling.get("class", "").split():
                    tables = sibling.xpath(".//table")
                    if tables:
                        fields = table_fields(tables[0])
                    break
                if sibling.tag == "a" and sibling.get("name"):
                    break
        items.append({**e, "metadata": fields, "anchor_found": bool(nodes), "metadata_found": bool(fields)})
    return items


def extract_apache(tree: html.HtmlElement, entries: list[dict]) -> list[dict]:
    items = []
    for e in entries:
        nodes = tree.xpath('//div[contains(concat(" ",normalize-space(@class)," ")," directive-section ")][h2[@id=$anchor]]', anchor=e["anchor"])
        index_anchor_found = bool(nodes)
        if not nodes:
            # An official index link can disagree with the module page anchor.
            # Preserve that discrepancy for audit, but recover the metadata.
            nodes = [section for section in tree.xpath('//div[contains(concat(" ",normalize-space(@class)," ")," directive-section ")]')
                     if any(tidy(span.text_content()).casefold() == e["name"].casefold()
                            for span in section.xpath('./h2/span'))]
        tables = nodes[0].xpath('.//table[contains(concat(" ",normalize-space(@class)," ")," directive ")]') if nodes else []
        fields = table_fields(tables[0]) if tables else {}
        items.append({**e, "metadata": fields, "anchor_found": index_anchor_found, "metadata_found": bool(fields), "index_anchor_mismatch": bool(nodes) and not index_anchor_found})
    return items


def gather() -> dict:
    print("Fetching official indexes...", flush=True)
    ng_index = page(NG + "dirindex.html")
    ap_index = page(AP + "mod/directives.html")
    ng_docs = page(NG)
    ap_modules = page(AP + "mod/")
    ng_entries = index_entries(ng_index, NG)
    ap_entries = index_entries(ap_index, AP + "mod/")
    nginx_variables = index_entries(page(NG + "varindex.html"), NG)

    ng_pages = set(e["module_url"] for e in ng_entries) | module_urls(ng_docs, NG, apache=False)
    ap_pages = set(e["module_url"] for e in ap_entries) | module_urls(ap_modules, AP + "mod/", apache=True)
    urls = sorted(ng_pages | ap_pages)
    print(f"Found {len(ng_entries)} nginx and {len(ap_entries)} Apache index rows; fetching {len(urls)} module pages.", flush=True)

    docs = {}
    failures = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=12) as pool:
        for i, (url, data, error) in enumerate(pool.map(fetch, urls), start=1):
            if error:
                failures[url] = error
            else:
                try:
                    docs[url] = html.fromstring(data)
                except (ValueError, html.ParserError) as exc:
                    failures[url] = f"HTML parser: {exc}"
            if i % 40 == 0 or i == len(urls):
                print(f"Inspected {i}/{len(urls)} module pages; failures {len(failures)}", flush=True)

    def enrich(entries: list[dict], extractor) -> list[dict]:
        groups = defaultdict(list)
        for e in entries:
            groups[e["module_url"]].append(e)
        result = []
        for url, group in groups.items():
            if url in docs:
                result += extractor(docs[url], group)
            else:
                result += [{**x, "metadata": {}, "anchor_found": False, "metadata_found": False} for x in group]
        return sorted(result, key=lambda x: (x["name"].casefold(), x["module_url"]))

    nginx = enrich(ng_entries, extract_nginx)
    apache = enrich(ap_entries, extract_apache)

    def metrics(items: list[dict], module_pages: set[str]) -> dict:
        return {
            "index_entries": len(items),
            "unique_names": len({e["name"].casefold() for e in items}),
            "referenced_pages": len({e["module_url"] for e in items}),
            "module_pages_in_scope": len(module_pages),
            "pages_downloaded": len(module_pages & docs.keys()),
            "pages_failed": len(module_pages & failures.keys()),
            "anchors_found": sum(e["anchor_found"] for e in items),
            "metadata_found": sum(e["metadata_found"] for e in items),
            "missing_metadata_examples": [e["name"] + " (" + e["module"] + ")" for e in items if not e["metadata_found"]][:15],
        }

    output = {
        "generated_at_utc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "source_indexes": {"nginx": NG + "dirindex.html", "nginx_variables": NG + "varindex.html", "apache": AP + "mod/directives.html"},
        "notice": "Unverified draft inventory. Index inclusion is NOT evidence of presence in a specific binary or version. Descriptive metadata may be incomplete. Do not generate configuration from this alone.",
        "metrics": {"nginx": metrics(nginx, ng_pages), "apache": metrics(apache, ap_pages)},
        "download_failures": failures,
        "nginx": nginx,
        "nginx_variables": nginx_variables,
        "apache": apache,
    }
    return output


def write_report(result: dict) -> None:
    n, a = result["metrics"]["nginx"], result["metrics"]["apache"]
    variables = result.get("nginx_variables", [])
    nginx_context = Counter(x["metadata"].get("context", "(not extracted)") for x in result["nginx"])
    apache_status = Counter(x["metadata"].get("status", "(not extracted)") for x in result["apache"])
    apache_context = Counter(x["metadata"].get("context", "(not extracted)") for x in result["apache"])
    report = f"""# Server Config Studio — nghiên cứu Nginx & Apache (21/11/2026)

## Kết quả kiểm kê tài liệu chính thức

Đây là bản kiểm kê tự động các **directive trong danh mục chính thức** và metadata từ trang module của mỗi dự án. Việc tải trang và trích xuất metadata là bằng chứng kiểm kê, **không đồng nghĩa đã kiểm chứng đầy đủ ngữ nghĩa của tất cả directive**. Các giới hạn được ghi rõ bên dưới.

| Chỉ số | Nginx | Apache HTTP Server 2.4 |
|---|---:|---:|
| Mục trong Directive Index | {n['index_entries']} | {a['index_entries']} |
| Tên directive phân biệt (không phân biệt hoa/thường) | {n['unique_names']} | {a['unique_names']} |
| Trang module được directive tham chiếu | {n['referenced_pages']} | {a['referenced_pages']} |
| Trang module tổng cộng trong phạm vi crawler | {n['module_pages_in_scope']} | {a['module_pages_in_scope']} |
| Trang module tải được | {n['pages_downloaded']} | {a['pages_downloaded']} |
| Trang module tải thất bại | {n['pages_failed']} | {a['pages_failed']} |
| Mục có anchor tìm thấy | {n['anchors_found']} | {a['anchors_found']} |
| Mục trích được bảng metadata | {n['metadata_found']} | {a['metadata_found']} |

Nginx Variable Index: **{len(variables)}** mục liên kết, **{len({v['name'].casefold() for v in variables})}** tên biến phân biệt; có thể trùng tên ở các context khác nhau. Các biến có prefix động (ví dụ `$http_...`) không thể liệt kê hết dưới dạng giá trị cụ thể.

Nguồn: [Nginx directive index]({NG}dirindex.html), [Nginx module index]({NG}), [Nginx configure]({NG}configure.html), [Apache directive index]({AP}mod/directives.html), [Apache module index]({AP}mod/), [Apache quick reference]({AP}mod/quickreference.html).

### Một số phân bố trích xuất

Apache status: {', '.join(f'`{key}`={val}' for key,val in apache_status.most_common(12))}.

Nginx context thường gặp (đếm nguyên chuỗi): {', '.join(f'`{key}`={val}' for key,val in nginx_context.most_common(12))}.

Apache context thường gặp: {', '.join(f'`{key}`={val}' for key,val in apache_context.most_common(10))}.

## Khác biệt mang tính kiến trúc

1. **Hai ngữ pháp phải độc lập**. Nginx có chỉ thị kết thúc bằng dấu chấm phẩy và block ngoặc nhọn; Apache thường mỗi directive một dòng và có container dạng `<VirtualHost>`, `<Directory>`, `<Location>`, `<Files>`, `<If>`.
2. **Một tên không nhất thiết tương ứng một directive duy nhất.** Nginx có thể dùng cùng tên trong HTTP và stream; ID registry nên là `(server, module, directive/anchor)`, không chỉ `name`.
3. **Khả dụng tùy build.** Nginx biên dịch một số module mặc định, một số chỉ khi `--with-...`; Apache có module tĩnh, module `LoadModule`, MPM, module phụ thuộc nền tảng.
4. **Metadata context chưa đủ để validate ngữ nghĩa.** Apache áp dụng các loại section theo thứ tự hợp nhất riêng; Nginx có quy tắc inherit riêng theo directive và version.
5. **Apache `.htaccess`** phải xét đồng thời context và nhóm `AllowOverride`/`AllowOverrideList`.
6. **Phiên bản phải xác thực bằng rule có nguồn.** Một directive được liệt kê trên trang hiện tại không đảm bảo dùng được trên bản Nginx/Apache cũ; Nginx Plus không được coi là Open Source.
7. **Config parser phải lossless.** Comment, include, whitespace, quoting/escaping, thứ tự rule, multiline, biến và directive chưa biết đều phải được bảo toàn khi import/export.
8. **Khối có ngữ pháp riêng** cần editor chuyên biệt hoặc raw editor: Nginx `map`, `geo`, `types`, `upstream`; Apache `<If>`, rewrite, `Require`, biểu thức `ap_expr`.

## Thiết kế registry đề xuất

Mỗi directive có: `id`, `server`, `module`, `name`, `source_url`, `syntax_raw`, `default_raw`, `contexts`, `minimum_version`, `maximum_version`, `build_requirements`, `platforms`, `arguments_schema`, `block_grammar`, `inheritance_rule`, `repeatability`, `warnings`, `examples`, `verification_state`.

Trong JSON kiểm kê hiện tại, các trường `metadata` lấy trực tiếp từ tài liệu. **Chưa có bảo đảm** rằng một chuỗi `Syntax` được máy trích xuất có thể được phân tích thành form đúng. `verification_state` của dữ liệu sơ bộ này phải được xem là `index-extracted`.

## Các công đoạn còn thiếu để đạt mức kiểm chứng đầy đủ

- Chuẩn hóa chính xác các bảng `Syntax`/`Default`/`Context` của từng module và đối chiếu với index.
- Tách từng lựa chọn tham số thành grammar/AST, bao gồm dấu ngoặc tùy chọn, lặp, bảng đặc biệt và phép thế biến.
- Đánh dấu từng directive theo OSS/Plus, build flags, platform, module và version. Trạng thái không rõ ràng thì cảnh báo, không tự mặc định là hỗ trợ.
- Dùng bộ mẫu config thực tế và test parse → serialize → parse để bảo đảm không mất dữ liệu.
- Với validator ngữ nghĩa, so sánh đầu ra trên binary Nginx/Apache theo version/profile tương ứng trong môi trường kiểm thử; tính năng này không cần tồn tại trong desktop app.
- Chấm điểm coverage theo **số directive được test**, không chỉ số dòng index đã tải.
- Kiểm tra diff khi trang chính thức thay đổi; sinh snapshot mới và review trước khi cập nhật ứng dụng.

## Tài liệu giải thích bắt buộc

- [Nginx configuration fundamentals]({NG}beginners_guide.html)
- [Nginx HTTP request processing]({NG}http/request_processing.html)
- [Nginx build options]({NG}configure.html)
- [Apache configuration syntax]({AP}configuring.html)
- [Apache configuration sections and merging]({AP}sections.html)
- [Apache expressions]({AP}expr.html)
- [Apache directive dictionary]({AP}mod/directive-dict.html)

## Giới hạn của snapshot

- Đếm **mục trong index**, không phải các syntax variant, tham số con, mọi biến và mọi thiết lập runtime.
- Nginx index có thể bao gồm tính năng trả phí; chỉ giữ chính thức Open Source trong profile OSS sau khi phân loại.
- Bộ module index Apache 2.4 gồm module theo nền tảng và những module không được build mặc định.
- Nếu trang/module tải lỗi, dữ liệu còn dấu vết ở `download_failures`; chưa được xem là đã nghiên cứu đầy đủ.
- Độ khớp anchor không nhất thiết đạt 100%: ví dụ mục Apache `<MDomainSet>` trong index trỏ tới `#mdomainset` nhưng trang module dùng `#mdomainsetsection`; crawler có thể dựa vào tên heading và gắn cờ sai lệch.
- Các trang how-to, kiến trúc và hướng dẫn bảo mật không được quét tự động toàn văn; đã xác định là nguồn cần nghiên cứu chuyên đề bổ sung.
"""
    (ROOT / "Server_Config_Studio_Research.md").write_text(report, encoding="utf-8")


if __name__ == "__main__":
    data = gather()
    out = ROOT / "Server_Config_Directive_Inventory.json"
    out.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    write_report(data)
    print("Wrote inventory and research report:", flush=True)
    print(json.dumps(data["metrics"], indent=2, ensure_ascii=False), flush=True)
    if data["download_failures"]:
        print("Failure sample:", list(data["download_failures"].items())[:5], flush=True)
