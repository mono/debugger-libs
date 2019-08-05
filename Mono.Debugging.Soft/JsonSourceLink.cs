using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mono.Debugging.Soft
{
	public class JsonSourceLink
	{
		[JsonProperty ("documents")]
		public Dictionary<string, string> Maps { get; set; }
	}
}
