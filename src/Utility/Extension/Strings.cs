using System;
using System.Collections.Generic;
using System.Linq;

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
				if (tmp[i] == wrd) {
					return true;
				}
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

		public static string ReplaceTo(this string str, Dictionary<string, string> dictionary) {
			string str2 = str.ToLower();
			string str3 = dictionary[str2];
			return str2.Replace(str2, str3);
		}

		public static string ReplaceTo(this string str, Dictionary<string, string[]> dictionary) {
			string str2 = str.ToLower();
			foreach (string key in dictionary.Keys) {
				if (dictionary[key].Contains(str2)) {
					return str.Replace(str2, key);
				}
			}
			return str2;
		}

		public static string GetString(this string[] array, char seperator = ',', bool newline = false) {
			string fullString = "";
			for (int i = 0; i < array.Length; i++) {
				if (i != array.Length - 1) {
					fullString += array[i] + (newline ? (seperator + "\n") : seperator);
					continue;
				}
				fullString += array[i];
			}
			return fullString;
		}
	}
}
