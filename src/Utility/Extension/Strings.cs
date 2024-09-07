using System;

namespace VSKingdom.Extension {
	internal static class StringExtension {
		public static string Replace(this string str, char[] sep, string val) {
			string[] tmp = str.Split(sep, StringSplitOptions.RemoveEmptyEntries);
			return String.Join(val, tmp);
		}

		public static string Replace(this string str, char[] sep, char val) {
			string[] tmp = str.Split(sep, StringSplitOptions.RemoveEmptyEntries);
			return String.Join(val, tmp);
		}

		public static bool Contains(this string str, char sep, string wrd) {
			string[] tmp = str.Split(sep, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < tmp.Length; i++) {
				if (tmp[i] == wrd) { return true; }
			}
			return false;
		}

		public static bool TooClose(this string str1, char seperator, int chances, string str2) {
			string[] temp = str1.Split(seperator, StringSplitOptions.RemoveEmptyEntries);
			string[] comp = str2.Split(seperator, StringSplitOptions.RemoveEmptyEntries);
			int fails = 0;
			for (int i = 0; i < temp.Length; i++) {
				for (int j = 0; j < comp.Length; j++) {
					if (temp[i].Length > 3 && temp[i] == comp[j]) {
						fails += 1;
					}
				}
			}
			return fails > chances;
		}

		public static bool TooClose(this string str1, char seperator, int chances, int leniency, string str2) {
			string[] temp = str1.Split(seperator, StringSplitOptions.RemoveEmptyEntries);
			string[] comp = str2.Split(seperator, StringSplitOptions.RemoveEmptyEntries);
			int fails = 0;
			for (int i = 0; i < temp.Length; i++) {
				for (int j = 0; j < comp.Length; j++) {
					if (temp[i].Length > leniency && temp[i] == comp[j]) {
						fails += 1;
					}
				}
			}
			return fails > chances;
		}
	}
}
