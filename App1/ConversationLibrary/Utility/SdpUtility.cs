namespace ConversationLibrary.Utility
{
    using Org.WebRtc;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    public static class SdpUtility
    {
        /// <summary>
        /// Heavily borrowed from the original sample with some mods - the original sample also did
        /// some work to pick a specific video codec and also to move VP8 to the head of the list
        /// but I've not done that yet.
        /// </summary>
        /// <param name="originalSdp"></param>
        /// <param name="audioCodecs"></param>
        /// <returns></returns>
        public static string FilterToSupportedCodecs(string originalSdp)
        {
            var filteredSdp = originalSdp;

            string[] incompatibleAudioCodecs =
                new string[] { "CN32000", "CN16000", "CN8000", "red8000", "telephone-event8000" };

            var compatibleCodecs = WebRTC.GetAudioCodecs().Where(
                codec => !incompatibleAudioCodecs.Contains(codec.Name + codec.ClockRate));

            Regex mfdRegex = new Regex("\r\nm=audio.*RTP.*?( .\\d*)+\r\n");
            Match mfdMatch = mfdRegex.Match(filteredSdp);

            List<string> mfdListToErase = new List<string>(); //mdf = media format descriptor

            bool audioMediaDescFound = mfdMatch.Groups.Count > 1; //Group 0 is whole match

            if (audioMediaDescFound)
            {
                for (int groupCtr = 1/*Group 0 is whole match*/; groupCtr < mfdMatch.Groups.Count; groupCtr++)
                {
                    for (int captureCtr = 0; captureCtr < mfdMatch.Groups[groupCtr].Captures.Count; captureCtr++)
                    {
                        mfdListToErase.Add(mfdMatch.Groups[groupCtr].Captures[captureCtr].Value.TrimStart());
                    }
                }
                mfdListToErase.RemoveAll(entry => compatibleCodecs.Any(c => c.Id.ToString() == entry));
            }

            if (audioMediaDescFound)
            {
                // Alter audio entry
                Regex audioRegex = new Regex("\r\n(m=audio.*RTP.*?)( .\\d*)+");
                filteredSdp = audioRegex.Replace(filteredSdp, "\r\n$1 " + string.Join(" ", compatibleCodecs.Select(c => c.Id)));
            }

            // Remove associated rtp mapping, format parameters, feedback parameters
            Regex removeOtherMdfs = new Regex("a=(rtpmap|fmtp|rtcp-fb):(" + String.Join("|", mfdListToErase) + ") .*\r\n");

            filteredSdp = removeOtherMdfs.Replace(filteredSdp, "");

            return (filteredSdp);
        }
    }
}
