using UnityEngine;

namespace HundredSchools.Combat
{
    /// <summary>
    /// WeaponUtils —— 武器系统共享工具类。
    ///
    /// 提供所有武器组件共用的静态方法，避免代码重复：
    ///   - Square / Circle / Triangle Sprite 全局缓存（程序化生成，零外部依赖）
    ///   - 鼠标世界坐标转换
    ///   - 从 PlayerMovement 读取当前学派
    /// </summary>
    public static class WeaponUtils
    {
        private static Sprite _cachedSquareSprite;
        private static Sprite _cachedCircleSprite;
        private static Sprite _cachedTriangleSprite;
        private static Sprite _cachedRingSprite;
        private static Sprite _cachedCrescentSprite;

        // ==================== Square ====================

        public static Sprite GetOrCreateSquareSprite()
        {
            if (_cachedSquareSprite != null)
                return _cachedSquareSprite;

            Texture2D tex = new Texture2D(4, 4);
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();

            _cachedSquareSprite = Sprite.Create(
                tex, new Rect(0f, 0f, 4f, 4f),
                new Vector2(0.5f, 0.5f), 4f
            );
            return _cachedSquareSprite;
        }

        // ==================== Circle ====================

        /// <summary>
        /// 程序化圆形 Sprite。64×64 像素，逐像素判断 dist ≤ radius。
        /// pixelsPerUnit = 64 → Sprite 直径 = 1 unit。
        /// </summary>
        public static Sprite GetOrCreateCircleSprite()
        {
            if (_cachedCircleSprite != null)
                return _cachedCircleSprite;

            int size = 64;
            float radius = size * 0.5f;
            Texture2D tex = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - radius + 0.5f;
                    float dy = y - radius + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = 1f - Mathf.Clamp01(dist - (radius - 1f));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _cachedCircleSprite = Sprite.Create(
                tex, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), size
            );
            return _cachedCircleSprite;
        }

        // ==================== Triangle ====================

        /// <summary>
        /// 程序化三角形 Sprite（尖角朝上）。
        ///
        /// 64×64 像素，用重心坐标法（Cross2D 同侧判定）绘制实心三角形。
        /// 顶点坐标：上(32, 60)、左下(4, 4)、右下(60, 4)。
        /// 边缘 1 像素做 alpha 渐变抗锯齿。
        /// pixelsPerUnit = 64 → Sprite 外接矩形 = 1×1 unit。
        ///
        /// 用途：道家 (Taoist) 敌人的灰模外形 —— 三角象征"飘忽不定、锐利灵动"。
        /// </summary>
        public static Sprite GetOrCreateTriangleSprite()
        {
            if (_cachedTriangleSprite != null)
                return _cachedTriangleSprite;

            int size = 64;
            Texture2D tex = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];

            Vector2 v0 = new Vector2(32f, 60f);
            Vector2 v1 = new Vector2(4f, 4f);
            Vector2 v2 = new Vector2(60f, 4f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = MinDistToTriangleEdges(p, v0, v1, v2);
                    // d >= 0 → 内部；d < 0 → 外部，用 Clamp 做边缘渐变
                    float alpha = d >= 0f ? 1f : 1f - Mathf.Clamp01(-d);
                    alpha = Mathf.Clamp01(alpha);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _cachedTriangleSprite = Sprite.Create(
                tex, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), size
            );
            return _cachedTriangleSprite;
        }

        /// <summary>点到三角形三条边的有符号距离（内部 ≥0，外部 <0）</summary>
        private static float MinDistToTriangleEdges(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross2D(p - a, b - a);
            float d2 = Cross2D(p - b, c - b);
            float d3 = Cross2D(p - c, a - c);

            bool hasNeg = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
            bool hasPos = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);

            if (!(hasNeg && hasPos))
            {
                return Mathf.Min(Mathf.Abs(d1), Mathf.Min(Mathf.Abs(d2), Mathf.Abs(d3)));
            }
            else
            {
                float minOut = 0f;
                if (d1 < 0f) minOut = Mathf.Min(minOut, d1);
                if (d2 < 0f) minOut = Mathf.Min(minOut, d2);
                if (d3 < 0f) minOut = Mathf.Min(minOut, d3);
                return minOut;
            }
        }

        private static float Cross2D(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        
        // ==================== Ring (圆环) ====================

        /// <summary>
        /// 程序化圆环 Sprite（空心圆，用于 Boss 波纹等特效）。
        /// 64x64 像素，环宽度约为外径的 20%，内外边缘均做抗锯齿。
        /// pixelsPerUnit = 64 → 外径 = 1 unit。
        /// </summary>
        public static Sprite GetOrCreateRingSprite()
        {
            if (_cachedRingSprite != null)
                return _cachedRingSprite;

            int size = 64;
            float outerR = size * 0.5f;
            float innerR = outerR * 0.75f;
            Texture2D tex = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - outerR + 0.5f;
                    float dy = y - outerR + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float alphaOuter = 1f - Mathf.Clamp01(dist - (outerR - 1f));
                    float alphaInner = Mathf.Clamp01(dist - (innerR - 1f));
                    float alpha = Mathf.Min(alphaOuter, alphaInner);

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _cachedRingSprite = Sprite.Create(
                tex, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), size
            );
            return _cachedRingSprite;
        }

        // ==================== Crescent（道家弹幕·弯月） ====================

        public static Sprite GetOrCreateCrescentSprite()
        {
            if (_cachedCrescentSprite != null)
                return _cachedCrescentSprite;

            int size = 64;
            float radius = size * 0.5f;
            Texture2D tex = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - radius;
                    float dy = y - radius;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // 外圆
                    float outerAlpha = 1f - Mathf.Clamp01(dist - (radius - 1f));
                    // 内切圆（偏移到左上方，切出月牙形）
                    float cutDx = x - (radius + 8f);
                    float cutDy = y - (radius - 4f);
                    float cutDist = Mathf.Sqrt(cutDx * cutDx + cutDy * cutDy);
                    float cutAlpha = 1f - Mathf.Clamp01(cutDist - (radius - 4f));

                    float alpha = Mathf.Max(0f, outerAlpha - cutAlpha);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _cachedCrescentSprite = Sprite.Create(
                tex, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), size
            );
            return _cachedCrescentSprite;
        }

        // ==================== 工具方法 ====================

        public static Vector3 GetMouseWorldPosition()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return Vector3.zero;

            Vector3 mouseScreen = Input.mousePosition;
            mouseScreen.z = -mainCam.transform.position.z;
            return mainCam.ScreenToWorldPoint(mouseScreen);
        }

        public static ESchool GetCurrentSchool(Component playerComponent)
        {
            Player.PlayerMovement pm = playerComponent.GetComponent<Player.PlayerMovement>();
            return pm != null ? pm.CurrentSchool : ESchool.Confucian;
        }

        /// <summary>学派 → 颜色映射（GDD v1.9）：儒金 / 法黑 / 道青 / 墨灰 / 无白</summary>
        public static Color GetSchoolColor(ESchool school)
        {
            switch (school)
            {
                case ESchool.Confucian: return Color.yellow;
                case ESchool.Legalist:  return Color.black;
                case ESchool.Taoist:    return Color.cyan;
                case ESchool.Mohist:    return Color.gray;
                case ESchool.None:      return Color.white;
                default:                return Color.white;
            }
        }
    }
}
