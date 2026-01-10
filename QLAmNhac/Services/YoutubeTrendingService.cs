using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web; // Dùng HttpRuntime.Cache
using System.Web.Configuration;
using Newtonsoft.Json.Linq;

namespace MusicSocialNetwork.Services
{
    public class YoutubeTrendingService
    {
        // Lấy API Key từ Web.config
        private static readonly string API_KEY = WebConfigurationManager.AppSettings["AIzaSyCL77_fuAu3pNZNEQebn5g00UfDVvpduRk"];
        private static readonly HttpClient client = new HttpClient();

        public static async Task<Dictionary<string, double>> GetTrendingMusicScoreAsync()
        {
            string cacheKey = "YoutubeTrendingScores_V2"; // Đổi tên key để tránh cache cũ
            var cachedData = HttpRuntime.Cache[cacheKey];

            if (cachedData != null)
            {
                return (Dictionary<string, double>)cachedData;
            }

            var result = new Dictionary<string, double>();

            // Thêm "snippet" để lấy ngày đăng (publishedAt)
            string url = $"https://www.googleapis.com/youtube/v3/videos?part=statistics,snippet&chart=mostPopular&videoCategoryId=10&regionCode=VN&maxResults=50&key={API_KEY}";

            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(json)["items"];

                    foreach (var item in data)
                    {
                        string videoId = item["id"].ToString();

                        // 1. Lấy chỉ số thống kê
                        long views = item["statistics"]["viewCount"]?.ToObject<long>() ?? 0;
                        long likes = item["statistics"]["likeCount"]?.ToObject<long>() ?? 0;

                        // 2. Lấy độ mới (Freshness)
                        DateTime publishDate = item["snippet"]["publishedAt"]?.ToObject<DateTime>() ?? DateTime.Now;
                        double daysOld = (DateTime.Now - publishDate).TotalDays;
                        if (daysOld < 1) daysOld = 1;

                        // 3. TÍNH ĐIỂM EXTERNAL (Tầng 1)
                        // Log10 giúp thu gọn số liệu: 1.000.000 views -> 6 điểm, 10.000 views -> 4 điểm
                        double popularity = (Math.Log10(views + 1) * 1.5) + (Math.Log10(likes + 1) * 0.5);

                        // Decay factor: Video càng cũ điểm càng giảm nhẹ
                        double freshnessFactor = 1.0 / (1.0 + (daysOld * 0.05));

                        // Điểm cuối cùng (Thang điểm khoảng 0 - 15)
                        result[videoId] = popularity * freshnessFactor;
                    }

                    // Cache 60 phút
                    HttpRuntime.Cache.Insert(cacheKey, result, null, DateTime.Now.AddMinutes(60), TimeSpan.Zero);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }

            return result;
        }
    }
}