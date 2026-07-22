"""
诸子百家·口诛笔伐 Demo — 灰模Sprite批量生成器
依据: 灰模资产规格清单 v1.0
生成: 全部39项视觉资产 (PNG格式, 透明背景)
"""

from PIL import Image, ImageDraw, ImageFilter
import math
import os

# ============================================================
# 颜色编码表 (来自规格 §2.1)
# ============================================================
COLORS = {
    "confucian_gold":    (212, 160, 23, 255),   # 儒金 #D4A017
    "confucian_bright":  (255, 215, 0, 255),     # 亮金 #FFD700
    "confucian_pillar":  (255, 236, 139, 255),   # 纯金光柱 #FFEC8B
    "confucian_dark":    (139, 105, 20, 255),    # 深金描边 #8B6914
    "legalist_black":    (26, 26, 26, 255),      # 法黑 #1A1A1A
    "legalist_dark":     (42, 42, 42, 255),      # 深灰+律令纹 #2A2A2A
    "legalist_pure":     (10, 10, 10, 255),      # 纯黑律令阵 #0A0A0A
    "legalist_border":   (74, 74, 74, 255),      # 灰白描边 #4A4A4A
    "daoist_teal":       (46, 139, 139, 255),    # 道青 #2E8B8B
    "daoist_bright":     (64, 224, 208, 255),    # 亮青 #40E0D0
    "daoist_water":      (0, 206, 209, 255),     # 青色水流 #00CED1
    "daoist_dark":       (26, 92, 92, 255),      # 深青描边 #1A5C5C
    "neutral_white":     (255, 255, 255, 255),   # 素白 #FFFFFF
    "neutral_grey":      (58, 58, 58, 255),      # 中灰 #3A3A3A (场地)
    "water_dark":        (42, 58, 58, 128),      # 深青灰半透明 #2A3A3A
    "cyclone_white":     (255, 255, 255, 51),    # 白色半透明 α=0.2
    "hp_green":          (76, 175, 80, 255),     # 绿 #4CAF50
    "hp_yellow":         (255, 193, 7, 255),     # 黄 #FFC107
    "hp_red":            (244, 67, 54, 255),     # 红 #F44336
    "stamina_blue":      (33, 150, 243, 255),    # 蓝 #2196F3
    "hud_bg_grey":       (42, 42, 42, 255),      # 深灰 #2A2A2A
    "hit_red":           (255, 48, 48, 255),     # 命中红 #FF3030
}

OUTPUT_BASE = r"D:\诸子百家_口诛笔伐\Modeul"

def ensure_dir(path):
    os.makedirs(path, exist_ok=True)

def new_image(size, bg=(0,0,0,0)):
    """创建透明背景画布, size=(w,h)"""
    return Image.new("RGBA", size, bg)

def save_png(img, category, name):
    """保存PNG到对应分类目录"""
    dirpath = os.path.join(OUTPUT_BASE, category)
    ensure_dir(dirpath)
    filepath = os.path.join(dirpath, f"{name}.png")
    img.save(filepath, "PNG")
    print(f"  [OK] {category}/{name}.png  ({img.size[0]}x{img.size[1]})")
    return filepath

# ============================================================
# 绘图工具函数
# ============================================================

def draw_filled_circle(draw, center, radius, color):
    """实心圆"""
    x, y = center
    draw.ellipse([x-radius, y-radius, x+radius, y+radius], fill=color)

def draw_ring(draw, center, outer_radius, border_width, color):
    """空心圆环"""
    x, y = center
    inner = outer_radius - border_width
    # 外圆
    draw.ellipse([x-outer_radius, y-outer_radius, x+outer_radius, y+outer_radius], fill=color)
    # 内圆(透明)
    draw.ellipse([x-inner, y-inner, x+inner, y+inner], fill=(0,0,0,0))

def draw_filled_square(draw, center, half_size, color):
    """实心方形"""
    x, y = center
    draw.rectangle([x-half_size, y-half_size, x+half_size, y+half_size], fill=color)

def draw_square_with_border(draw, center, half_size, border_width, fill_color, border_color):
    """带描边方形"""
    x, y = center
    # 外层(描边色)
    draw.rectangle([x-half_size, y-half_size, x+half_size, y+half_size], fill=border_color)
    # 内层(填充色)
    inner = half_size - border_width
    draw.rectangle([x-inner, y-inner, x+inner, y+inner], fill=fill_color)

def draw_hexagon(draw, center, radius, color, border_width=0, border_color=None):
    """六边形"""
    x, y = center
    points = []
    for i in range(6):
        angle = math.pi / 3 * i - math.pi / 2  # 顶点朝上
        px = x + radius * math.cos(angle)
        py = y + radius * math.sin(angle)
        points.append((px, py))
    if border_width > 0 and border_color:
        # 画外层(描边色)的大六边形
        outer_points = []
        for i in range(6):
            angle = math.pi / 3 * i - math.pi / 2
            px = x + (radius + border_width) * math.cos(angle)
            py = y + (radius + border_width) * math.sin(angle)
            outer_points.append((px, py))
        draw.polygon(outer_points, fill=border_color)
    draw.polygon(points, fill=color)

def draw_triangle(draw, center, base, height, color, border_width=0, border_color=None):
    """锐角三角形(箭头形) - 描边使用轮廓线方案"""
    x, y = center
    # 等腰三角形, 顶点朝上
    apex = (x, y - height/2)
    left = (x - base/2, y + height/2)
    right = (x + base/2, y + height/2)
    points = [apex, left, right]
    if border_width > 0 and border_color:
        # 先画一个稍大的三角形作为描边
        # 三角形描边: 用外扩顶点
        scale_offsets = [
            (0, -border_width),  # 顶点向外
            (-border_width, border_width),  # 左下
            (border_width, border_width),   # 右下
        ]
        outer_points = [(p[0]+o[0], p[1]+o[1]) for p, o in zip(points, scale_offsets)]
        draw.polygon(outer_points, fill=border_color)
    draw.polygon(points, fill=color)

def draw_rectangle(draw, center, w, h, color):
    """矩形"""
    x, y = center
    draw.rectangle([x-w/2, y-h/2, x+w/2, y+h/2], fill=color)

def draw_sector(draw, center, radius, angle_deg, color):
    """扇形"""
    x, y = center
    # 扇形: 从 -angle/2 到 +angle/2, 朝右
    points = [(x, y)]
    steps = 30
    half = angle_deg / 2
    for i in range(steps + 1):
        a = math.radians(-half + (angle_deg * i / steps))
        px = x + radius * math.cos(a)
        py = y + radius * math.sin(a)
        points.append((px, py))
    draw.polygon(points, fill=color)

def draw_arc_segment(draw, center, radius, arc_width, color):
    """弧线段(月牙形)"""
    x, y = center
    # 外弧
    outer_points = []
    inner_points = []
    steps = 30
    start_angle = -60
    end_angle = 60
    for i in range(steps + 1):
        a = math.radians(start_angle + (end_angle - start_angle) * i / steps)
        outer_points.append((x + radius * math.cos(a), y + radius * math.sin(a)))
    for i in range(steps + 1):
        a = math.radians(end_angle - (end_angle - start_angle) * i / steps)
        r = radius - arc_width
        inner_points.append((x + r * math.cos(a), y + r * math.sin(a)))
    points = outer_points + inner_points
    draw.polygon(points, fill=color)

def draw_dashed_ring(draw, center, radius, border_width, color, dash_len=8, gap_len=4):
    """虚线圆环"""
    x, y = center
    circumference = 2 * math.pi * radius
    num_dashes = int(circumference / (dash_len + gap_len))
    for i in range(num_dashes):
        start_frac = i * (dash_len + gap_len) / circumference
        end_frac = (i * (dash_len + gap_len) + dash_len) / circumference
        start_angle = start_frac * 2 * math.pi - math.pi / 2
        end_angle = end_frac * 2 * math.pi - math.pi / 2
        # 画一段弧
        steps = 10
        points = []
        for j in range(steps + 1):
            a = start_angle + (end_angle - start_angle) * j / steps
            points.append((x + radius * math.cos(a), y + radius * math.sin(a)))
        for j in range(steps + 1):
            a = end_angle - (end_angle - start_angle) * j / steps
            r = radius - border_width
            points.append((x + r * math.cos(a), y + r * math.sin(a)))
        draw.polygon(points, fill=color)

# ============================================================
# 1. 玩家角色 (4项)
# ============================================================
def gen_player_assets():
    print("\n=== 生成玩家角色 Sprite ===")
    
    # player_base: 白色圆形, 直径48px
    size = 56  # 48 + 抗锯齿余量
    img = new_image((size, size))
    draw = ImageDraw.Draw(img)
    draw_filled_circle(draw, (size//2, size//2), 24, COLORS["neutral_white"])
    img = img.filter(ImageFilter.GaussianBlur(0.5))
    save_png(img, "玩家角色", "player_base")
    
    # player_ring_confucian: 儒金圆环描边, 外径52px, 环宽3px
    for school, color_key in [("confucian", "confucian_gold"), 
                               ("legalist", "legalist_black"),
                               ("daoist", "daoist_teal")]:
        size = 60
        img = new_image((size, size))
        draw = ImageDraw.Draw(img)
        draw_ring(draw, (size//2, size//2), 26, 3, COLORS[color_key])
        img = img.filter(ImageFilter.GaussianBlur(0.3))
        save_png(img, "玩家角色", f"player_ring_{school}")

# ============================================================
# 2. 弟子 (6项)
# ============================================================
def gen_disciple_assets():
    print("\n=== 生成弟子 Sprite ===")
    
    disciples = [
        ("disciple_confucian_normal", 36, "confucian_gold", None, 0),
        ("disciple_confucian_elite",  44, "confucian_gold", "confucian_dark", 4),
        ("disciple_legalist_normal",  36, "legalist_black", None, 0),
        ("disciple_legalist_elite",   44, "legalist_black", "legalist_border", 4),
        ("disciple_daoist_normal",    36, "daoist_teal", None, 0),
        ("disciple_daoist_elite",     44, "daoist_teal", "daoist_dark", 4),
    ]
    
    for name, px_size, fill_key, border_key, bw in disciples:
        padding = bw + 4
        total = px_size + padding * 2
        img = new_image((total, total))
        draw = ImageDraw.Draw(img)
        half = px_size // 2
        center = (total // 2, total // 2)
        if border_key:
            draw_square_with_border(draw, center, half, bw, COLORS[fill_key], COLORS[border_key])
        else:
            draw_filled_square(draw, center, half, COLORS[fill_key])
        img = img.filter(ImageFilter.GaussianBlur(0.3))
        save_png(img, "弟子", name)

# ============================================================
# 3. Boss (3宗师 × 3阶段 = 9项)
# ============================================================
def gen_boss_assets():
    print("\n=== 生成 Boss Sprite ===")
    
    bosses = [
        # (name_prefix, [(phase, size, color_key), ...])
        ("boss_confucian", [
            (1, 80, "confucian_gold"),
            (2, 90, "confucian_bright"),
            (3, 100, "confucian_pillar"),
        ]),
        ("boss_legalist", [
            (1, 80, "legalist_black"),
            (2, 90, "legalist_dark"),
            (3, 100, "legalist_pure"),
        ]),
        ("boss_daoist", [
            (1, 80, "daoist_teal"),
            (2, 85, "daoist_bright"),
            (3, 95, "daoist_water"),
        ]),
    ]
    
    for prefix, phases in bosses:
        for phase, px_size, color_key in phases:
            name = f"{prefix}_phase{phase}"
            total = px_size + 8
            img = new_image((total, total))
            draw = ImageDraw.Draw(img)
            draw_hexagon(draw, (total//2, total//2), px_size//2, COLORS[color_key])
            img = img.filter(ImageFilter.GaussianBlur(0.5))
            save_png(img, "Boss", name)

# ============================================================
# 4. 弹幕 (13项)
# ============================================================
def gen_bullet_assets():
    print("\n=== 生成弹幕 Sprite ===")
    
    # --- 玩家弹幕 ---
    # 射艺普通箭矢: 窄长矩形 24x6
    img = new_image((32, 14))
    draw = ImageDraw.Draw(img)
    draw_rectangle(draw, (16, 7), 24, 6, COLORS["neutral_white"])
    save_png(img, "弹幕", "bullet_archery_arrow")
    
    # 射艺蓄力箭: 窄长矩形+发光 32x8, 带外发光
    img = new_image((44, 18))
    draw = ImageDraw.Draw(img)
    # 外发光
    draw_rectangle(draw, (22, 9), 36, 12, (255, 255, 255, 80))
    # 主体
    draw_rectangle(draw, (22, 9), 32, 8, COLORS["neutral_white"])
    save_png(img, "弹幕", "bullet_archery_charge")
    
    # 御艺冲刺带: 宽矩形带状, 48px宽, 渐变
    img = new_image((100, 56))
    draw = ImageDraw.Draw(img)
    # 渐变效果: 多个半透明矩形叠加
    for i in range(10):
        alpha = int(102 * (1 - i/10))  # 从0.4到0
        color = (255, 255, 255, alpha)
        draw_rectangle(draw, (50, 28), 90 - i*8, 48, color)
    save_png(img, "弹幕", "bullet_yu_dash_trail")
    
    # 礼击推力波: 扇形 90度, 半径96px (中心朝右, 像向右推)
    img = new_image((130, 130))
    draw = ImageDraw.Draw(img)
    # 圆心在左侧, 扇形向右展开(像从玩家身体推出去的推力)
    # 圆心 (20, 65), 90度扇形从 -45 到 +45 (向右)
    cx, cy = 20, 65
    points = [(cx, cy)]
    steps = 30
    for i in range(steps + 1):
        a = math.radians(-45 + (90 * i / steps))
        px = cx + 96 * math.cos(a)
        py = cy + 96 * math.sin(a)
        points.append((px, py))
    draw.polygon(points, fill=COLORS["neutral_white"])
    img = img.filter(ImageFilter.GaussianBlur(0.5))
    save_png(img, "弹幕", "bullet_li_push_wave")
    
    # 礼屏障: 空心圆环, 半径64px, 描边4px
    img = new_image((140, 140))
    draw = ImageDraw.Draw(img)
    draw_ring(draw, (70, 70), 64, 4, COLORS["neutral_white"])
    save_png(img, "弹幕", "bullet_li_barrier")
    
    # 礼屏障刺: 屏障内短线 (8根随机短线)
    img = new_image((140, 140))
    draw = ImageDraw.Draw(img)
    import random
    random.seed(42)
    for _ in range(8):
        angle = random.uniform(0, 2 * math.pi)
        r1 = random.uniform(20, 55)
        r2 = r1 + 8
        x1 = 70 + r1 * math.cos(angle)
        y1 = 70 + r1 * math.sin(angle)
        x2 = 70 + r2 * math.cos(angle)
        y2 = 70 + r2 * math.sin(angle)
        draw.line([(x1, y1), (x2, y2)], fill=COLORS["neutral_white"], width=2)
    save_png(img, "弹幕", "bullet_li_barrier_thorn")
    
    # 礼反弹圈: 扩散圆环, 描边3px
    img = new_image((140, 140))
    draw = ImageDraw.Draw(img)
    draw_ring(draw, (70, 70), 64, 3, COLORS["neutral_white"])
    save_png(img, "弹幕", "bullet_li_reflect_circle")
    
    # --- 敌人弹幕 ---
    # 儒家扩散弹: 圆形, 直径16px
    img = new_image((24, 24))
    draw = ImageDraw.Draw(img)
    draw_filled_circle(draw, (12, 12), 8, COLORS["confucian_gold"])
    save_png(img, "弹幕", "bullet_confucian_spread")
    
    # 儒家弹命中溅射圈: 半径64px空心
    img = new_image((136, 136))
    draw = ImageDraw.Draw(img)
    draw_ring(draw, (68, 68), 64, 2, (212, 160, 23, 150))
    save_png(img, "弹幕", "bullet_confucian_spread_splash")
    
    # 法家直线弹: 锐角三角形, 底12px 高20px, 白描边1px
    img = new_image((20, 28))
    draw = ImageDraw.Draw(img)
    # 三角形指向右(箭头形), 长边竖直, 顶点朝右
    apex_right = (18, 14)           # 顶点朝右
    left_top = (2, 4)               # 左上
    left_bot = (2, 24)              # 左下
    if True:
        # 描边: 稍微外扩
        draw.polygon([(20, 14), (0, 2), (0, 26)], fill=COLORS["neutral_white"])
        draw.polygon([apex_right, left_top, left_bot], fill=COLORS["legalist_black"])
    save_png(img, "弹幕", "bullet_legalist_line")
    
    # 道家弧线弹: 弧线段(月牙), 弧长32px 弧宽8px
    img = new_image((44, 44))
    draw = ImageDraw.Draw(img)
    draw_arc_segment(draw, (22, 22), 20, 8, COLORS["daoist_teal"])
    save_png(img, "弹幕", "bullet_daoist_arc")
    
    # --- Boss弹幕 ---
    # Boss儒家扩散弹: 大圆形, 直径24px, 更亮
    img = new_image((32, 32))
    draw = ImageDraw.Draw(img)
    draw_filled_circle(draw, (16, 16), 12, COLORS["confucian_bright"])
    save_png(img, "弹幕", "bullet_boss_confucian_spread")
    
    # Boss儒家溅射圈: 半径96px (比弟子更大)
    img = new_image((200, 200))
    draw = ImageDraw.Draw(img)
    draw_ring(draw, (100, 100), 96, 3, (255, 215, 0, 150))
    save_png(img, "弹幕", "bullet_boss_confucian_splash")
    
    # Boss法家追踪弹: 大锐角三角形, 底18px 高28px, 白描边2px
    img = new_image((34, 36))
    draw = ImageDraw.Draw(img)
    # 指向右的箭头
    draw.polygon([(34, 18), (2, 0), (2, 36)], fill=COLORS["neutral_white"])
    draw.polygon([(30, 18), (4, 4), (4, 32)], fill=COLORS["legalist_black"])
    save_png(img, "弹幕", "bullet_boss_legalist_track")
    
    # Boss道宗师波纹: 扩散空心圆环, 描边3px, 半透明
    img = new_image((140, 140))
    draw = ImageDraw.Draw(img)
    draw_ring(draw, (70, 70), 64, 3, (46, 139, 139, 179))  # α=0.7
    save_png(img, "弹幕", "bullet_boss_daoist_ripple")

# ============================================================
# 5. 场地与拾取物 (4项)
# ============================================================
def gen_arena_assets():
    print("\n=== 生成场地与拾取物 Sprite ===")
    
    # arena_ground: 灰色平面 1280x1280 (实际用Tile平铺, 这里生成128x128的tile)
    img = new_image((128, 128), COLORS["neutral_grey"])
    save_png(img, "场地与拾取物", "arena_ground_tile")
    
    # arena_ground_full: 完整20x20单位地面 (1280x1280)
    img = new_image((1280, 1280), COLORS["neutral_grey"])
    save_png(img, "场地与拾取物", "arena_ground_full")
    
    # arena_water: 深青灰半透明底色 1280x1280
    img = new_image((1280, 1280), COLORS["water_dark"])
    save_png(img, "场地与拾取物", "arena_water_full")
    
    # arena_water_tile: 浅水tile
    img = new_image((128, 128), COLORS["water_dark"])
    save_png(img, "场地与拾取物", "arena_water_tile")
    
    # arena_cyclone_zone: 虚线圆环, 半径128px(2单位), 白色半透明
    img = new_image((280, 280))
    draw = ImageDraw.Draw(img)
    draw_dashed_ring(draw, (140, 140), 128, 3, COLORS["cyclone_white"])
    save_png(img, "场地与拾取物", "arena_cyclone_zone")
    
    # pickup_knowledge: 白色小圆点, 直径8px
    img = new_image((16, 16))
    draw = ImageDraw.Draw(img)
    draw_filled_circle(draw, (8, 8), 4, COLORS["neutral_white"])
    save_png(img, "场地与拾取物", "pickup_knowledge")

# ============================================================
# 6. HUD元素 (7项)
# ============================================================
def gen_hud_assets():
    print("\n=== 生成 HUD 元素 Sprite ===")
    
    # HP条背景: 200x16 深灰
    img = new_image((200, 16), COLORS["hud_bg_grey"])
    save_png(img, "HUD元素", "hud_hp_bar_bg")
    
    # HP条填充-绿: 200x16
    img = new_image((200, 16), COLORS["hp_green"])
    save_png(img, "HUD元素", "hud_hp_bar_fill_green")
    
    # HP条填充-黄: 200x16
    img = new_image((200, 16), COLORS["hp_yellow"])
    save_png(img, "HUD元素", "hud_hp_bar_fill_yellow")
    
    # HP条填充-红: 200x16
    img = new_image((200, 16), COLORS["hp_red"])
    save_png(img, "HUD元素", "hud_hp_bar_fill_red")
    
    # 体力条背景: 150x10 深灰
    img = new_image((150, 10), COLORS["hud_bg_grey"])
    save_png(img, "HUD元素", "hud_stamina_bar_bg")
    
    # 体力条填充: 150x10 蓝
    img = new_image((150, 10), COLORS["stamina_blue"])
    save_png(img, "HUD元素", "hud_stamina_bar_fill")
    
    # 学识图标: 小白色光点(用于HUD计数器旁)
    img = new_image((20, 20))
    draw = ImageDraw.Draw(img)
    draw_filled_circle(draw, (10, 10), 6, COLORS["neutral_white"])
    # 外发光
    draw_ring(draw, (10, 10), 8, 1, (255, 255, 255, 100))
    save_png(img, "HUD元素", "hud_knowledge_icon")
    
    # 波次指示器图标
    img = new_image((24, 24))
    draw = ImageDraw.Draw(img)
    draw_rectangle(draw, (12, 12), 20, 4, COLORS["neutral_white"])
    save_png(img, "HUD元素", "hud_wave_indicator")
    
    # Boss阶段指示器图标
    img = new_image((24, 24))
    draw = ImageDraw.Draw(img)
    draw_hexagon(draw, (12, 12), 10, COLORS["neutral_white"])
    save_png(img, "HUD元素", "hud_boss_phase_icon")

# ============================================================
# 7. 特效Sprite (命中闪烁状态 + 死亡碎裂碎片)
# ============================================================
def gen_vfx_assets():
    print("\n=== 生成特效 Sprite ===")
    
    # 命中闪烁-白色状态(叠加用)
    img = new_image((48, 48), COLORS["neutral_white"])
    save_png(img, "弹幕", "vfx_hit_flash_white")
    
    # 命中闪烁-红色状态(叠加用)
    img = new_image((48, 48), COLORS["hit_red"])
    save_png(img, "弹幕", "vfx_hit_flash_red")
    
    # 死亡碎裂碎片 (5种小碎片, 4-8px)
    import random
    random.seed(99)
    for i in range(5):
        size = random.randint(4, 8)
        img = new_image((size+2, size+2))
        draw = ImageDraw.Draw(img)
        draw_filled_square(draw, (size//2+1, size//2+1), size//2, COLORS["neutral_white"])
        save_png(img, "弹幕", f"vfx_death_shatter_{i+1}")

# ============================================================
# 主入口
# ============================================================
if __name__ == "__main__":
    print("=" * 60)
    print("诸子百家·口诛笔伐 Demo — 灰模Sprite生成器")
    print("依据: 灰模资产规格清单 v1.0")
    print("=" * 60)
    
    gen_player_assets()
    gen_disciple_assets()
    gen_boss_assets()
    gen_bullet_assets()
    gen_arena_assets()
    gen_hud_assets()
    gen_vfx_assets()
    
    print("\n" + "=" * 60)
    print("全部Sprite生成完成!")
    print(f"输出目录: {OUTPUT_BASE}")
    print("=" * 60)
