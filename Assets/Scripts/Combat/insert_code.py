import sys
filepath = r'D:\诸子百家_口诛笔伐\Assets\Scripts\Combat\ProjectileBase.cs'
with open(filepath, 'r', encoding='utf-8') as f:
    content = f.read()

print("File length:", len(content))
print("Lines:", content.count('\n'))
sys.exit(0)
