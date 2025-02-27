using BizHawk.Emulation.Common;

using System.IO;

namespace BizHawk.Emulation.Cores.Nintendo.Gameboy
{
	partial class GambatteLink
	{
		void ICodeDataLogger.SetCDL(ICodeDataLog cdl)
		{
			((ICodeDataLogger)L).SetCDL(cdl);
		}

		void ICodeDataLogger.NewCDL(ICodeDataLog cdl)
		{
			((ICodeDataLogger)L).NewCDL(cdl);
		}

		void ICodeDataLogger.DisassembleCDL(Stream s, ICodeDataLog cdl) { ((ICodeDataLogger)L).DisassembleCDL(s, cdl); }

	}
}