import fitz
from pathlib import Path
base = Path(r'D:\Projects 20260720\TMCWD')
pdf = base / 'BILLING.pdf'
out = base / 'billing_page1.png'
doc = fitz.open(str(pdf))
page = doc[0]
mat = fitz.Matrix(2, 2)
pix = page.get_pixmap(matrix=mat)
pix.save(str(out))
print('pages=', doc.page_count)
print('saved=', out)
print('exists=', out.exists(), 'size=', out.stat().st_size)
