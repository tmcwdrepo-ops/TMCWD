from pathlib import Path
from docx import Document
from docx.shared import Pt, Inches
from docx.enum.text import WD_PARAGRAPH_ALIGNMENT
from docx.oxml.shared import OxmlElement
from docx.oxml.ns import qn
from docx.dml.color import ColorFormat

base = Path(r'D:\Projects 20260720\TMCWD')
md_path = base / 'Billing_Collection_Update_Documentation.md'
out_path = base / 'Billing_Collection_Update_Documentation.docx'

# Add a simple horizontal rule by drawing a paragraph with border

def add_hr(doc):
    p = doc.add_paragraph()
    p.alignment = WD_PARAGRAPH_ALIGNMENT.CENTER
    p = doc.add_paragraph()
    p.paragraph_format.left_indent = Inches(0)
    p.paragraph_format.right_indent = Inches(0)
    p.paragraph_format.space_before = Pt(12)
    p.paragraph_format.space_after = Pt(12)
    p.style = 'Normal'
    p._p.append(OxmlElement('w:pPr'))
    ppr = p._p.find(qn('w:pPr'))
    border = OxmlElement('w:pBdr')
    bottom = OxmlElement('w:bottom')
    bottom.set(qn('w:val'), 'single')
    bottom.set(qn('w:sz'), '4')
    bottom.set(qn('w:space'), '1')
    bottom.set(qn('w:color'), 'A6A6A6')
    border.append(bottom)
    ppr.append(border)


doc = Document()
# set margins
for section in doc.sections:
    section.top_margin = Inches(0.8)
    section.bottom_margin = Inches(0.8)
    section.left_margin = Inches(1.0)
    section.right_margin = Inches(1.0)

# Cover page
cover = doc.add_paragraph()
cover.alignment = WD_PARAGRAPH_ALIGNMENT.CENTER
cover.space_before = Inches(0.3)
cover.space_after = Inches(0.2)
run = cover.add_run('BILLING AND COLLECTIONS\nWEB APPLICATION DEV UPDATE')
run.bold = True
run.font.name = 'Arial'
run.font.size = Pt(25)
run.font.color.rgb = (0x3E, 0x8D, 0xD1)

p = doc.add_paragraph()
p.alignment = WD_PARAGRAPH_ALIGNMENT.CENTER
run = p.add_run('Trece Martires City Water District')
run.italic = True
run.font.name = 'Arial'
run.font.size = Pt(18)
run.font.color.rgb = (0x2F, 0x2F, 0x2F)

p = doc.add_paragraph()
p.alignment = WD_PARAGRAPH_ALIGNMENT.LEFT
run = p.add_run('Prepared By: ')
run.bold = True
run.font.name = 'Arial'
run.font.size = Pt(13)
run = p.add_run('Wally Villanueva')
run.font.name = 'Arial'
run.font.size = Pt(13)
run.bold = True

p = doc.add_paragraph()
p.alignment = WD_PARAGRAPH_ALIGNMENT.LEFT
p.space_before = Inches(1.0)
run = p.add_run('Reviewed and Approved By:')
run.font.name = 'Arial'
run.font.size = Pt(12)

p = doc.add_paragraph()
p.alignment = WD_PARAGRAPH_ALIGNMENT.LEFT
p.space_before = Inches(0.15)
run = p.add_run('______________________________')
run.font.name = 'Arial'
run.font.size = Pt(12)

p = doc.add_paragraph()
p.alignment = WD_PARAGRAPH_ALIGNMENT.LEFT
run = p.add_run('Edvan O. Reyes')
run.font.name = 'Arial'
run.font.size = Pt(13)
run.bold = True
p = doc.add_paragraph()
p.alignment = WD_PARAGRAPH_ALIGNMENT.LEFT
run = p.add_run('OIC - ADMIN GSO')
run.font.name = 'Arial'
run.font.size = Pt(11)
run.italic = True

# Add a horizontal rule-like separator at the end of cover page
add_hr(doc)

doc.add_page_break()

# Content body from markdown, excluding the title line and using normal formatting
text = md_path.read_text(encoding='utf-8')
for line in text.splitlines():
    if not line.strip():
        doc.add_paragraph('')
        continue
    if line.startswith('## '):
        p = doc.add_paragraph()
        run = p.add_run(line[3:])
        run.bold = True
        run.font.name = 'Arial'
        run.font.size = Pt(14)
        p.space_before = Pt(12)
        p.space_after = Pt(8)
    elif line.startswith('### '):
        p = doc.add_paragraph()
        run = p.add_run(line[4:])
        run.bold = True
        run.font.name = 'Arial'
        run.font.size = Pt(12)
        p.space_before = Pt(8)
        p.space_after = Pt(6)
    elif line.startswith('- '):
        p = doc.add_paragraph(style='List Bullet')
        p.paragraph_format.left_indent = Inches(0.3)
        p.paragraph_format.space_after = Pt(2)
        run = p.add_run(line[2:])
        run.font.name = 'Arial'
        run.font.size = Pt(11)
    elif line.startswith('1. '):
        p = doc.add_paragraph(style='List Number')
        p.paragraph_format.left_indent = Inches(0.3)
        p.paragraph_format.space_after = Pt(2)
        run = p.add_run(line[3:])
        run.font.name = 'Arial'
        run.font.size = Pt(11)
    else:
        p = doc.add_paragraph(line)
        p.paragraph_format.space_after = Pt(4)
        p.runs[0].font.name = 'Arial'
        p.runs[0].font.size = Pt(11)

# Save
# remove any accidental extra blank first pages if present
# just save final doc

doc.save(out_path)
print(f'Created: {out_path}')
print(f'Exists: {out_path.exists()}')
print(f'Size: {out_path.stat().st_size} bytes')
