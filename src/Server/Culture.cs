using ProtoBuf;
using System.Collections.Generic;

namespace VSKingdom {
	[ProtoContract]
	public class Culture {
		public string Name;
		public string Lang;

		public List<Kingdom> kingdoms;

		// Default Skin, Hair, and Eye colors.
		public List<string> skinColors;
		public List<string> hairColors;
		public List<string> eyesColors;

		// Default Hair and Bear styles.
		public List<string> hairStyles;
		public List<string> faceStyles;

		// Acceptable Blocks to use.
		public List<string> treeBlocks;
		public List<string> woodBlocks;
		public List<string> rockBlocks;
	}
}