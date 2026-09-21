from pathlib import Path
from pypdf import PdfReader

base = Path(r'D:\Projects 20260720\TMCWD')
pdf = base / 'BILLING.pdf'
out = base / 'BILLING_extracted.txt'

reader = PdfReader(str(pdf))
parts = []
for i, page in enumerate(reader.pages, 1):
    text = page.extract_text() or ''
    parts.append(f'--- PAGE {i} ---\n{text}\n--- END PAGE ---\n')

out.write_text('\n'.join(parts), encoding='utf-8')
print(f'PDF pages: {len(reader.pages)}')
print(f'Extracted: {out}')
print(f'Exists: {out.exists()}')
print(f'Size: {out.stat().st_size} bytes')
