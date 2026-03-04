import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
from pypdf import PdfReader
r = PdfReader(r"c:\CRMs\InvektoServices\ideas\docs\saglik-senaryolari-formatted.pdf")
print(f"Pages: {len(r.pages)}")
for i, p in enumerate(r.pages):
    text = p.extract_text()
    print(f"Page {i+1}: {len(text)} chars")
    print(f"  Preview: {text[:300]}...")
    print()
