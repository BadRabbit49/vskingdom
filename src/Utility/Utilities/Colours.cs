using System;
using System.Linq;
using System.Drawing;
using Vintagestory.API.Util;

namespace VSKingdom.Utilities {
	internal static class ColoursUtil {
		public static string RandomCode(int minHue = 0, int maxHue = 255) {
			if (minHue < 000) { minHue = 000; }
			if (maxHue > 255) { maxHue = 255; }
			Random rnd = new Random();
			int colorR = rnd.Next(minHue, maxHue);
			int colorG = rnd.Next(minHue, maxHue);
			int colorB = rnd.Next(minHue, maxHue);
			Color srgb = Color.FromArgb(colorR, colorG, colorB);
			string hex = srgb.R.ToString("X2") + srgb.G.ToString("X2") + srgb.B.ToString("X2");
			return "#" + hex;
		}

		public static Color ColorFromHSV(double hue, double saturation, double value) {
			int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
			double f = hue / 60 - Math.Floor(hue / 60);
			value = value * 255;
			int v = Convert.ToInt32(value);
			int p = Convert.ToInt32(value * (1 - saturation));
			int q = Convert.ToInt32(value * (1 - f * saturation));
			int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));
			switch (hi) {
				case 0: return Color.FromArgb(255, v, t, p);
				case 1: return Color.FromArgb(255, q, v, p);
				case 2: return Color.FromArgb(255, p, v, t);
				case 3: return Color.FromArgb(255, p, q, v);
				case 4: return Color.FromArgb(255, t, p, v);
				default: return Color.FromArgb(255, v, p, q);
			}
		}
		
		public static string GetHexCode(string color) {
			if (color.StartsWith('#')) {
				char[] hex = color.Where(c => (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '-')).ToArray();
				if (hex.Length >= 6) {
					return new string(new char[] { '#', hex[0], hex[1], hex[2], hex[3], hex[4], hex[5] });
				}
			}
			string fixedCode = color.ToLowerInvariant().RemoveDiacritics().Replace("-", "").Replace("_", "");
			switch (fixedCode) {
				case "black": return "#0d0d0d";
				case "bloodred": return "#590000";
				case "blue": return "#115691";
				case "brightgreen": return "#7ee673";
				case "brightred": return "#ff3030";
				case "brown": return "#4f290d";
				case "darkblue": return "#05335c";
				case "darkbrown": return "#261307";
				case "darkgray": return "#454545";
				case "darkgreen": return "#0b2e12";
				case "darkgrey": return "#353535";
				case "darkpink": return "#964792";
				case "darkpurple": return "#6a007a";
				case "darkred": return "#630c06";
				case "darkyellow": return "#a69712";
				case "deepred": return "#290300";
				case "forestgreen": return "#26422c";
				case "gray": return "#707070";
				case "green": return "#36753c";
				case "grey": return "#606060";
				case "honey": return "#ffb300";
				case "jeanblue": return "#142636";
				case "lightblue": return "#3a9cf2";
				case "lightbrown": return "#735948";
				case "lightgray": return "#a8a8a8";
				case "lightgreen": return "#5a9967";
				case "lightgrey": return "#8f8f8f";
				case "lightpink": return "#ffa8fb";
				case "lightpurple": return "#a46aad";
				case "lightred": return "#fc5f53";
				case "lightyellow": return "#d9d18d";
				case "magenta": return "#eb0056";
				case "navyblue": return "#091b2b";
				case "orange": return "#d65611";
				case "pink": return "#ff69f7";
				case "purple": return "#a018b5";
				case "red": return "#f0190a";
				case "skyblue": return "#73b0e6";
				case "vanta": return "#000000";
				case "white": return "#ffffff";
				case "yellow": return "#f7e223";
				case "random": return string.Format("#{0:X6}", new Random().Next(0x1000000));
				default: return "#ffffff";
			}
		}
	}
}