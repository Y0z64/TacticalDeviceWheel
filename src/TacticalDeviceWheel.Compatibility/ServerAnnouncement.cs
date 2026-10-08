using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;
using SPT.Common.Models.Logging;
using SPT.Common.Utils;

namespace TacticalDeviceWheel.Compatibility;

internal static class ServerAnnouncement
{
	public static string Status { get; private set; } = "not_started";


	public static async Task SendAsync(Action<string> info, Action<string> warning)
	{
		if (Status != "not_started")
		{
			return;
		}
		Status = "pending";
		try
		{
			ServerLogRequest val = new ServerLogRequest
			{
				Source = "Tactical Device Wheel",
				Message = "模组：Tactical Device Wheel 版本0.4.3 (GUID: com.xunhuaizhuo.tdw | targets SPT: 4.1.x) 作者：巡怀浊 / xunhuaizhuo 已加载（客户端）",
				Level = (EServerLogLevel)3,
				Color = (EServerLogTextColor)37,
				BackgroundColor = (EServerLogBackgroundColor)49
			};
			string obj = await RequestHandler.PostJsonAsync("/singleplayer/log", Json.Serialize<ServerLogRequest>(val)).ConfigureAwait(continueOnCapturedContext: false);
			if (string.IsNullOrWhiteSpace(obj))
			{
				throw new InvalidOperationException("Empty server response.");
			}
			JObject val2 = JObject.Parse(obj);
			if (val2["err"] != null && (int)val2["err"] != 0)
			{
				throw new InvalidOperationException("Server rejected the log request: " + (string)val2["errmsg"]);
			}
			Status = "sent";
			info("TDW SERVER INFO sent: 模组：Tactical Device Wheel 版本0.4.3 (GUID: com.xunhuaizhuo.tdw | targets SPT: 4.1.x) 作者：巡怀浊 / xunhuaizhuo 已加载（客户端）");
		}
		catch (Exception ex)
		{
			Status = "failed";
			warning("TDW Server 加载信息发送失败；客户端功能继续运行。" + ex);
		}
	}
}
