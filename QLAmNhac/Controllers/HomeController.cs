using MusicSocialNetwork.Services;
using QLAmNhac.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace MusicSocialNetwork.Controllers
{
    public class HomeController : Controller
    {
        private QLAmNhac_Web1Entities1 db = new QLAmNhac_Web1Entities1();

        public async Task<ActionResult> Index()
        {
            // --- BƯỚC 1: LẤY TẦNG 1 (EXTERNAL TRENDING) ---
            // Kết quả trả về là Dictionary<VideoID, Score>
            var youtubeScores = await YoutubeTrendingService.GetTrendingMusicScoreAsync();


            // --- BƯỚC 2: LẤY DỮ LIỆU TẦNG 2 TỪ DB (INTERNAL RAW DATA) ---
            // Chỉ lấy dữ liệu thô cần thiết, không tính toán phức tạp trong SQL
            var rawSongs = db.BaiHats
                .Where(s => s.TrangThaiDuyet == 1) // Chỉ lấy bài đã duyệt
                .OrderByDescending(s => s.NgayDang)
                .Take(200) // Chỉ xét 200 bài mới nhất để tối ưu hiệu năng
                .Select(s => new
                {
                    Song = s,
                    TotalVotes = s.TagVotes.Count(),
                    NgayDang = s.NgayDang,
                    // Consensus: Lấy số vote của Tag cao nhất (để xem mọi người có đồng quan điểm ko)
                    MaxTagConsensus = s.TagVotes
                        .GroupBy(v => v.MaTag)
                        .Select(g => g.Count())
                        .OrderByDescending(c => c)
                        .FirstOrDefault()
                })
                .ToList(); // Kết thúc truy vấn SQL tại đây


            // --- BƯỚC 3: TÍNH TOÁN TẦNG 2 (INTERNAL) & TẦNG 3 (FUSION) ---
            // Xử lý trong RAM (In-Memory)

            var trendingList = rawSongs.Select(item =>
            {
                // A. TÍNH ĐIỂM INTERNAL (Tầng 2)

                // 1. Freshness (Độ mới): Bài mới < 30 ngày được ưu tiên
                double daysOld = (DateTime.Now - (item.NgayDang ?? DateTime.Now)).TotalDays;
                if (daysOld < 0) daysOld = 0;
                double freshnessScore = (daysOld <= 30) ? (30 - daysOld) / 5.0 : 0; // Max 6 điểm

                // 2. TagVotes (Số lượng): Logarit để tránh spam (100 vote không gấp đôi 50 vote)
                double voteScore = Math.Log10(item.TotalVotes + 1) * 5.0;

                // 3. Consensus (Chất lượng): Tỷ lệ đồng thuận (0.0 -> 1.0)
                // Nếu 10 người vote và 9 người chọn "Rock" -> Consensus cao -> Tốt
                double consensusRatio = (item.TotalVotes > 0)
                    ? (double)item.MaxTagConsensus / item.TotalVotes
                    : 0;

                double internalScore = (voteScore * (1 + consensusRatio)) + freshnessScore;


                // B. TÍNH ĐIỂM FUSION (Tầng 3)

                // Trích xuất ID từ Link Database
                string videoId = GetYoutubeID(item.Song.LinkYoutube);
                double externalScore = 0;

                // Kiểm tra xem bài này có nằm trong Top YouTube không
                if (!string.IsNullOrEmpty(videoId) && youtubeScores.ContainsKey(videoId))
                {
                    externalScore = youtubeScores[videoId];
                }

                // CÔNG THỨC FUSION:
                // Nếu bài hát có External Score (đang hot trên YT), nó sẽ được cộng thêm điểm rất lớn
                // Internal Score đảm bảo bài hát được cộng đồng quan tâm vẫn có chỗ đứng
                double finalScore = (internalScore * 0.4) + (externalScore * 2.0); // External hệ số cao hơn vì nó phản ánh Trend thực tế

                return new
                {
                    Song = item.Song,
                    FinalScore = finalScore,
                    DebugInfo = $"Int: {internalScore:F2} | Ext: {externalScore:F2} | Days: {daysOld:F0}" // Để debug nếu cần
                };
            })
            .OrderByDescending(x => x.FinalScore) // Sắp xếp theo điểm tổng hợp
            .Take(12) // Lấy top 12
            .Select(x => x.Song)
            .ToList();

            // --- MỚI: TẠO DANH SÁCH 24 BÀI HÁT NGẪU NHIÊN (không trùng với trendingList) ---
            var trendingIds = trendingList.Select(s => s.MaBaiHat).ToList();

            var randomSongs = db.BaiHats
                .Where(b => b.TrangThaiDuyet == 1 && !trendingIds.Contains(b.MaBaiHat))
                .OrderBy(x => Guid.NewGuid()) // Random
                .Take(24)
                .ToList();

            ViewBag.RandomSongs = randomSongs;

            return View(trendingList);
        }

        // Hàm Helper tách ID Youtube (Chạy trong RAM, không ảnh hưởng DB)
        private string GetYoutubeID(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            try
            {
                url = url.Trim();
                if (url.Contains("v=")) return url.Split(new[] { "v=" }, StringSplitOptions.None)[1].Split('&')[0];
                if (url.Contains("youtu.be/")) return url.Split(new[] { "youtu.be/" }, StringSplitOptions.None)[1].Split('?')[0];
                if (url.Contains("/embed/")) return url.Split(new[] { "/embed/" }, StringSplitOptions.None)[1].Split('?')[0];
            }
            catch { return ""; }
            return "";
        }

        public ActionResult Search(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return RedirectToAction("Index");
            }

            // 1. Chuẩn hóa từ khóa của người dùng
            string tuKhoaGoc = query.Trim(); // Giữ nguyên để hiển thị ra View

            // Tạo "phiên bản sạch" của từ khóa: chữ thường, không cách, không gạch nối
            // VD: "V - Pop" -> "vpop"
            string tuKhoaSach = tuKhoaGoc.ToLower().Replace(" ", "").Replace("-", "");

            // 2. Truy vấn Database
            var ketQua = db.BaiHats
                           .Where(s => s.TrangThaiDuyet == 1 && (
                                // Tìm Tên bài hoặc Ca sĩ theo cách thông thường
                                s.TenBaiHat.Contains(tuKhoaGoc) ||
                                s.NguoiTrinhBay.Contains(tuKhoaGoc) ||

                                // TÌM THEO TAG (LOGIC THÔNG MINH)
                                s.TagVotes.Any(tv =>
                                    // Cách 1: Tìm chính xác (VD: gõ "Pop" ra "Pop")
                                    tv.Tag.TenTag.Contains(tuKhoaGoc) ||

                                    // Cách 2: Tìm mờ (Fuzzy Search)
                                    // "Làm sạch" tên tag trong DB rồi mới so sánh với từ khóa sạch
                                    // Lưu ý: Entity Framework sẽ dịch dòng này sang SQL REPLACE()
                                    tv.Tag.TenTag.Replace(" ", "").Replace("-", "").Contains(tuKhoaSach)
                                )
                           ))
                           .OrderByDescending(s => s.NgayDang)
                           .ToList();

            ViewBag.Keyword = query;
            return View(ketQua);
        }

        public ActionResult About()
        {
            ViewBag.Message = "Mạng xã hội âm nhạc Crowdsourcing.";
            return View();
        }
    }
}