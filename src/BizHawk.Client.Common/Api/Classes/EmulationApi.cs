﻿using System;
using System.Collections.Generic;
using System.ComponentModel;

using BizHawk.Emulation.Common;
using BizHawk.Emulation.Cores.Consoles.Nintendo.QuickNES;
using BizHawk.Emulation.Cores.Consoles.Sega.gpgx;
using BizHawk.Emulation.Cores.Nintendo.BSNES;
using BizHawk.Emulation.Cores.Nintendo.NES;
using BizHawk.Emulation.Cores.Consoles.Nintendo.NDS;
using BizHawk.Emulation.Cores.Nintendo.SNES;
using BizHawk.Emulation.Cores.PCEngine;
using BizHawk.Emulation.Cores.Sega.MasterSystem;
using BizHawk.Emulation.Cores.WonderSwan;

namespace BizHawk.Client.Common
{
	[Description("A library for interacting with the currently loaded emulator core")]
	public sealed class EmulationApi : IEmulationApi
	{
		[RequiredService]
		private IEmulator Emulator { get; set; }

		[OptionalService]
		private IBoardInfo BoardInfo { get; set; }

		[OptionalService]
		private IDebuggable DebuggableCore { get; set; }

		[OptionalService]
		private IDisassemblable DisassemblableCore { get; set; }

		[OptionalService]
		private IInputPollable InputPollableCore { get; set; }

		[OptionalService]
		private IMemoryDomains MemoryDomains { get; set; }

		[OptionalService]
		private IRegionable RegionableCore { get; set; }

		private readonly Config _config;

		private readonly IGameInfo _game;

		private readonly Action<string> LogCallback;

		/// <summary>Using this property to get a reference to the global <see cref="Config"/> instance is a terrible, horrible, no good, very bad idea. That's why it's not in the <see cref="IEmulationApi">interface</see>.</summary>
		public Config ForbiddenConfigReference
		{
			get
			{
				ForbiddenConfigReferenceUsed = true;
				return _config;
			}
		}

		public Action FrameAdvanceCallback { get; set; }

		public bool ForbiddenConfigReferenceUsed { get; private set; }

		public Action YieldCallback { get; set; }

		public EmulationApi(Action<string> logCallback, Config config, IGameInfo game)
		{
			_config = config;
			_game = game;
			LogCallback = logCallback;
		}

		public void DisplayVsync(bool enabled) => _config.VSync = enabled;

		public void FrameAdvance() => FrameAdvanceCallback();

		public int FrameCount() => Emulator.Frame;

		public object Disassemble(uint pc, string name = "")
		{
			try
			{
				if (DisassemblableCore != null)
				{
					return new {
						disasm = DisassemblableCore.Disassemble(
							string.IsNullOrEmpty(name) ? MemoryDomains.SystemBus : MemoryDomains[name],
							pc,
							out var l
						),
						length = l
					};
				}
			}
			catch (NotImplementedException) {}
			LogCallback($"Error: {Emulator.Attributes().CoreName} does not yet implement {nameof(IDisassemblable.Disassemble)}()");
			return null;
		}

		public ulong? GetRegister(string name)
		{
			try
			{
				if (DebuggableCore != null)
				{
					var registers = DebuggableCore.GetCpuFlagsAndRegisters();
					return registers.ContainsKey(name) ? registers[name].Value : (ulong?) null;
				}
			}
			catch (NotImplementedException) {}
			LogCallback($"Error: {Emulator.Attributes().CoreName} does not yet implement {nameof(IDebuggable.GetCpuFlagsAndRegisters)}()");
			return null;
		}

		public Dictionary<string, ulong> GetRegisters()
		{
			try
			{
				if (DebuggableCore != null)
				{
					var table = new Dictionary<string, ulong>();
					foreach (var kvp in DebuggableCore.GetCpuFlagsAndRegisters()) table[kvp.Key] = kvp.Value.Value;
					return table;
				}
			}
			catch (NotImplementedException) {}
			LogCallback($"Error: {Emulator.Attributes().CoreName} does not yet implement {nameof(IDebuggable.GetCpuFlagsAndRegisters)}()");
			return new Dictionary<string, ulong>();
		}

		public void SetRegister(string register, int value)
		{
			try
			{
				if (DebuggableCore != null)
				{
					DebuggableCore.SetCpuRegister(register, value);
					return;
				}
			}
			catch (NotImplementedException) {}
			LogCallback($"Error: {Emulator.Attributes().CoreName} does not yet implement {nameof(IDebuggable.SetCpuRegister)}()");
		}

		public long TotalExecutedCycles()
		{
			try
			{
				if (DebuggableCore != null) return DebuggableCore.TotalExecutedCycles;
			}
			catch (NotImplementedException) {}
			LogCallback($"Error: {Emulator.Attributes().CoreName} does not yet implement {nameof(IDebuggable.TotalExecutedCycles)}()");
			return default;
		}

		public string GetSystemId() => _game.System;

		public bool IsLagged()
		{
			if (InputPollableCore != null) return InputPollableCore.IsLagFrame;
			LogCallback($"Can not get lag information, {Emulator.Attributes().CoreName} does not implement {nameof(IInputPollable)}");
			return false;
		}

		public void SetIsLagged(bool value = true)
		{
			if (InputPollableCore != null) InputPollableCore.IsLagFrame = value;
			else LogCallback($"Can not set lag information, {Emulator.Attributes().CoreName} does not implement {nameof(IInputPollable)}");
		}

		public int LagCount()
		{
			if (InputPollableCore != null) return InputPollableCore.LagCount;
			LogCallback($"Can not get lag information, {Emulator.Attributes().CoreName} does not implement {nameof(IInputPollable)}");
			return default;
		}

		public void SetLagCount(int count)
		{
			if (InputPollableCore != null) InputPollableCore.LagCount = count;
			else LogCallback($"Can not set lag information, {Emulator.Attributes().CoreName} does not implement {nameof(IInputPollable)}");
		}

		public void LimitFramerate(bool enabled) => _config.ClockThrottle = enabled;

		public void MinimizeFrameskip(bool enabled) => _config.AutoMinimizeSkipping = enabled;

		public void Yield() => YieldCallback();

		public string GetDisplayType() => (RegionableCore?.Region)?.ToString() ?? "";

		public string GetBoardName() => BoardInfo?.BoardName ?? "";

		public object GetSettings() => Emulator switch
		{
			GPGX gpgx => gpgx.GetSettings(),
			LibsnesCore snes => snes.GetSettings(),
			NES nes => nes.GetSettings(),
			NDS nds => nds.GetSettings(),
			PCEngine pce => pce.GetSettings(),
			QuickNES quickNes => quickNes.GetSettings(),
			SMS sms => sms.GetSettings(),
			WonderSwan ws => ws.GetSettings(),
			_ => null
		};

		public PutSettingsDirtyBits PutSettings(object settings) => Emulator switch
		{
			GPGX gpgx => gpgx.PutSettings((GPGX.GPGXSettings) settings),
			LibsnesCore snes => snes.PutSettings((LibsnesCore.SnesSettings) settings),
			NES nes => nes.PutSettings((NES.NESSettings) settings),
			NDS nds => nds.PutSettings((NDS.NDSSettings) settings),
			PCEngine pce => pce.PutSettings((PCEngine.PCESettings) settings),
			QuickNES quickNes => quickNes.PutSettings((QuickNES.QuickNESSettings) settings),
			SMS sms => sms.PutSettings((SMS.SmsSettings) settings),
			WonderSwan ws => ws.PutSettings((WonderSwan.Settings) settings),
			_ => PutSettingsDirtyBits.None
		};

		public void SetRenderPlanes(params bool[] args)
		{
			static bool GetSetting(bool[] settings, int index) => index >= settings.Length || settings[index];
			void SetLibsnes(LibsnesCore core)
			{
				var s = core.GetSettings();
				s.ShowBG1_0 = s.ShowBG1_1 = GetSetting(args, 0);
				s.ShowBG2_0 = s.ShowBG2_1 = GetSetting(args, 1);
				s.ShowBG3_0 = s.ShowBG3_1 = GetSetting(args, 2);
				s.ShowBG4_0 = s.ShowBG4_1 = GetSetting(args, 3);
				s.ShowOBJ_0 = GetSetting(args, 4);
				s.ShowOBJ_1 = GetSetting(args, 5);
				s.ShowOBJ_2 = GetSetting(args, 6);
				s.ShowOBJ_3 = GetSetting(args, 7);
				core.PutSettings(s);
			}
			void SetBsnes(BsnesCore core)
			{
				var s = core.GetSettings();
				// TODO: This should probably support both prios inidividually but I have no idea whether changing this breaks anything
				s.ShowBG1_0 = s.ShowBG1_1 = GetSetting(args, 0);
				s.ShowBG2_0 = s.ShowBG2_1 = GetSetting(args, 1);
				s.ShowBG3_0 = s.ShowBG3_1 = GetSetting(args, 2);
				s.ShowBG4_0 = s.ShowBG4_1 = GetSetting(args, 3);
				s.ShowOBJ_0 = GetSetting(args, 4);
				s.ShowOBJ_1 = GetSetting(args, 5);
				s.ShowOBJ_2 = GetSetting(args, 6);
				s.ShowOBJ_3 = GetSetting(args, 7);
				core.PutSettings(s);
			}
			void SetCygne(WonderSwan ws)
			{
				var s = ws.GetSettings();
				s.EnableSprites = GetSetting(args, 0);
				s.EnableFG = GetSetting(args, 1);
				s.EnableBG = GetSetting(args, 2);
				ws.PutSettings(s);
			}
			void SetGPGX(GPGX core)
			{
				var s = core.GetSettings();
				s.DrawBGA = GetSetting(args, 0);
				s.DrawBGB = GetSetting(args, 1);
				s.DrawBGW = GetSetting(args, 2);
				s.DrawObj = GetSetting(args, 3);
				core.PutSettings(s);
			}
			void SetNesHawk(NES core)
			{
				var s = core.GetSettings();
				// in the future, we could do something more arbitrary here, but this isn't any worse than the old system
				s.DispSprites = GetSetting(args, 0);
				s.DispBackground = GetSetting(args, 1);
				core.PutSettings(s);
			}
			void SetPCEHawk(PCEngine pce)
			{
				var s = pce.GetSettings();
				s.ShowOBJ1 = GetSetting(args, 0);
				s.ShowBG1 = GetSetting(args, 1);
				if (args.Length > 2)
				{
					s.ShowOBJ2 = GetSetting(args, 2);
					s.ShowBG2 = GetSetting(args, 3);
				}
				pce.PutSettings(s);
			}
			void SetQuickNES(QuickNES quicknes)
			{
				var s = quicknes.GetSettings();
				// this core doesn't support disabling BG
				var showSp = GetSetting(args, 0);
				if (showSp && s.NumSprites == 0) s.NumSprites = 8;
				else if (!showSp && s.NumSprites > 0) s.NumSprites = 0;
				quicknes.PutSettings(s);
			}
			void SetSmsHawk(SMS sms)
			{
				var s = sms.GetSettings();
				s.DispOBJ = GetSetting(args, 0);
				s.DispBG = GetSetting(args, 1);
				sms.PutSettings(s);
			}
			switch (Emulator)
			{
				case GPGX gpgx:
					SetGPGX(gpgx);
					break;
				case LibsnesCore snes:
					SetLibsnes(snes);
					break;
				case BsnesCore bsnes:
					SetBsnes(bsnes);
					break;
				case NES nes:
					SetNesHawk(nes);
					break;
				case PCEngine pce:
					SetPCEHawk(pce);
					break;
				case QuickNES quicknes:
					SetQuickNES(quicknes);
					break;
				case SMS sms:
					SetSmsHawk(sms);
					break;
				case WonderSwan ws:
					SetCygne(ws);
					break;
			}
		}
	}
}
