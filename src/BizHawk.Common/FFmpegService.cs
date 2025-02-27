﻿using System;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

namespace BizHawk.Common
{
	public static class FFmpegService
	{
		public static string FFmpegPath = string.Empty; // always updated in DiscoHawk.Program/EmuHawk.Program

		//could return a different version for different operating systems.. shouldnt be hard.
		public static readonly string Version = "N-92462-g529debc987";

		//likewise
		public static readonly string Url = "https://github.com/TASEmulators/ffmpeg-binaries/blob/master/ffmpeg-20181118-529debc-win64-static_ffmpeg.7z?raw=true";

		public class AudioQueryResult
		{
			public bool IsAudio;
		}

		private static string[] Escape(IEnumerable<string> args)
		{
			return args.Select(s => s.Contains(" ") ? $"\"{s}\"" : s).ToArray();
		}

		//note: accepts . or : in the stream stream/substream separator in the stream ID format, since that changed at some point in FFMPEG history
		//if someone has a better idea how to make the determination of whether an audio stream is available, I'm all ears
		private static readonly Regex rxHasAudio = new Regex(@"Stream \#(\d*(\.|\:)\d*)\: Audio", RegexOptions.Compiled);
		public static AudioQueryResult QueryAudio(string path)
		{
			var ret = new AudioQueryResult();
			string stdout = Run("-i", path).Text;
			ret.IsAudio = rxHasAudio.Matches(stdout).Count > 0;
			return ret;
		}

		/// <summary>
		/// queries whether this service is available. if ffmpeg is broken or missing, then you can handle it gracefully
		/// </summary>
		public static bool QueryServiceAvailable()
		{
			try
			{
				string stdout = Run("-version").Text;
				if (stdout.Contains($"ffmpeg version {Version}")) return true;
			}
			catch
			{
			}
			return false;
		}

		public struct RunResults
		{
			public string Text;
			public int ExitCode;
		}

		public static RunResults Run(params string[] args)
		{
			args = Escape(args);
			StringBuilder sbCmdline = new StringBuilder();
			for (int i = 0; i < args.Length; i++)
			{
				sbCmdline.Append(args[i]);
				if (i != args.Length - 1) sbCmdline.Append(' ');
			}

			ProcessStartInfo oInfo = new ProcessStartInfo(FFmpegPath, sbCmdline.ToString())
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};

			string result = "";

			//probably naturally thread-safe...
			//make a lock if not
			Action<StreamReader,bool> readerloop = (reader,ignore) =>
			{
				string line = "";
				int idx = 0;
				for (; ; )
				{
					int c = reader.Read();
					if (c == -1)
						break;
					else if (c == '\r')
						idx = 0;
					else if (c == '\n')
					{
						if(!ignore)
							lock(oInfo)
								result += line + "\n";
						line = "";
						idx = 0;
					}
					else
					{
						if (idx < line.Length)
							line = line.Substring(0, idx) + (char)c + line.Substring(idx + 1);
						else
							line += (char)c;
						idx++;
					}
				}

				if(!ignore)
					if(line != "")
						lock(oInfo)
							result += line + "\n"; //not sure about the final \n but i concat it after each finished line so why not.. whatever
			};

			Process proc = Process.Start(oInfo);

			var tout = new Thread(() => readerloop(proc.StandardOutput,false));
			var terr = new Thread(() => readerloop(proc.StandardError,false));

			tout.Start();
			terr.Start();

			proc.WaitForExit();

			return new RunResults
			{
				ExitCode = proc.ExitCode,
				Text = result
			};
		}

		/// <exception cref="InvalidOperationException">FFmpeg exited with non-zero exit code or produced no output</exception>
		public static byte[] DecodeAudio(string path)
		{
			string tempfile = Path.GetTempFileName();
			try
			{
				var runResults = Run("-i", path, "-xerror", "-f", "wav", "-ar", "44100", "-ac", "2", "-acodec", "pcm_s16le", "-y", tempfile);
				if(runResults.ExitCode != 0)
					throw new InvalidOperationException($"Failure running ffmpeg for audio decode. here was its output:\r\n{runResults.Text}");
				byte[] ret = File.ReadAllBytes(tempfile);
				if (ret.Length == 0)
					throw new InvalidOperationException($"Failure running ffmpeg for audio decode. here was its output:\r\n{runResults.Text}");
				return ret;
			}
			finally
			{
				File.Delete(tempfile);
			}
		}
	}

}