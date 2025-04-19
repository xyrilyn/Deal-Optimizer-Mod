using UnityEngine;

namespace DealOptimizer_IL2CPP
{
	public class UIUtils()
	{
        public static Rect ClampToScreen(Rect r)
        {
            r.x = Mathf.Clamp(r.x, 0, Screen.width - r.width);
            r.y = Mathf.Clamp(r.y, 0, Screen.height - r.height);
            return r;
        }

        public static Texture2D MakeTexture(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];

            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }
    }
}
