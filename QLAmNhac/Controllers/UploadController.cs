using QLAmNhac.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using System.Web.Script.Serialization; // Thư viện quan trọng để decode JSON

namespace QLAmNhac.Controllers
{
    public class UploadController : Controller
    {
        private QLAmNhac_Web1Entities1 db = new QLAmNhac_Web1Entities1();

        // GET: Hiển thị form đăng nhạc
        public ActionResult Create()
        {
            // 1. Kiểm tra đăng nhập & Quyền
            if (Session["User"] == null) return RedirectToAction("Login", "Account");
            var user = Session["User"] as NguoiDung;
            if (user.PhanQuyen != 1 && user.PhanQuyen != 2) return RedirectToAction("Index", "Home");

            // 2. Lấy danh sách Tag để hiển thị checkbox
            ViewBag.Tags = db.Tags.ToList();

            return View();
        }

        // --- Trang Quản lý nhạc cá nhân (Cho Nghệ Sĩ) ---
        public ActionResult MySongs()
        {
            if (Session["User"] == null) return RedirectToAction("Login", "Account");
            var user = Session["User"] as NguoiDung;

            // Lấy danh sách bài hát do chính user này upload
            var mySongs = db.BaiHats
                            .Where(s => s.NguoiUpload == user.MaNguoiDung)
                            .OrderByDescending(s => s.NgayDang)
                            .ToList();

            return View(mySongs);
        }

        // POST: Xử lý lưu bài hát
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(BaiHat model, int[] selectedTags)
        {
            var user = Session["User"] as NguoiDung;
            if (user == null || (user.PhanQuyen != 1 && user.PhanQuyen != 2)) return RedirectToAction("Index", "Home");

            // Load lại Tag nếu có lỗi
            ViewBag.Tags = db.Tags.ToList();

            if (ModelState.IsValid)
            {
                // 1. Xử lý Link Youtube lấy ID
                string cleanId = ExtractYoutubeID(model.LinkYoutube);
                if (string.IsNullOrEmpty(cleanId))
                {
                    ModelState.AddModelError("LinkYoutube", "Link YouTube không hợp lệ!");
                    return View(model);
                }
                model.LinkYoutube = cleanId;

                // 2. XỬ LÝ NGƯỜI TRÌNH BÀY (AUTO YOUTUBE CHANNEL)
                if (string.IsNullOrEmpty(model.NguoiTrinhBay))
                {
                    // Gọi hàm lấy tên kênh từ YouTube (Đã sửa lỗi font)
                    string channelName = GetYoutubeChannelName(cleanId);
                    if (!string.IsNullOrEmpty(channelName))
                    {
                        model.NguoiTrinhBay = channelName;
                    }
                    else
                    {
                        model.NguoiTrinhBay = user.HoTen;
                    }
                }

                // 3. Các thông tin khác
                model.NguoiUpload = user.MaNguoiDung;
                model.NgayDang = DateTime.Now;

                if (user.PhanQuyen == 2)
                {
                    model.TrangThaiDuyet = 1;
                    model.GhiChuAdmin = "Admin đăng tải";
                }
                else
                {
                    model.TrangThaiDuyet = 0;
                }

                // 4. Lưu Bài hát
                db.BaiHats.Add(model);
                db.SaveChanges();

                // 5. Lưu Tag
                if (selectedTags != null && selectedTags.Length > 0)
                {
                    foreach (var tagId in selectedTags)
                    {
                        TagVote vote = new TagVote();
                        vote.MaBaiHat = model.MaBaiHat;
                        vote.MaTag = tagId;
                        vote.MaNguoiDung = user.MaNguoiDung;
                        vote.NgayVote = DateTime.Now;
                        db.TagVotes.Add(vote);
                    }
                    db.SaveChanges();
                }

                TempData["Success"] = model.TrangThaiDuyet == 1 ? "Đăng bài thành công!" : "Đã gửi bài, vui lòng chờ duyệt.";
                return RedirectToAction("Index", "Home");
            }

            return View(model);
        }
        // --- SỬA VÀ XÓA BÀI HÁT ---

        // 1. GET: Hiển thị form Sửa
        public ActionResult Edit(int id)
        {
            // Check đăng nhập
            if (Session["User"] == null) return RedirectToAction("Login", "Account");
            var user = Session["User"] as NguoiDung;

            // Tìm bài hát
            var song = db.BaiHats.Find(id);
            if (song == null) return HttpNotFound();

            // Check quyền: Chỉ chính chủ (NguoiUpload) hoặc Admin mới được sửa
            if (song.NguoiUpload != user.MaNguoiDung && user.PhanQuyen != 2)
            {
                TempData["Error"] = "Bạn không có quyền sửa bài hát này!";
                return RedirectToAction("MySongs");
            }

            // Load tất cả Tag để hiển thị
            ViewBag.Tags = db.Tags.ToList();

            // Load các Tag mà user này ĐÃ CHỌN cho bài hát này (để tick sẵn)
            // Logic: Lấy danh sách MaTag trong bảng TagVote ứng với User và Bài hát này
            var selectedTagIds = db.TagVotes
                                   .Where(tv => tv.MaBaiHat == id && tv.MaNguoiDung == user.MaNguoiDung)
                                   .Select(tv => tv.MaTag)
                                   .ToList();

            ViewBag.SelectedTags = selectedTagIds;

            return View(song);
        }

        // 2. POST: Lưu thay đổi
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(BaiHat model, int[] selectedTags)
        {
            var user = Session["User"] as NguoiDung;
            if (user == null) return RedirectToAction("Login", "Account");

            // Load lại ViewBag phòng trường hợp lỗi trả về View
            ViewBag.Tags = db.Tags.ToList();
            ViewBag.SelectedTags = selectedTags != null ? selectedTags.ToList() : new List<int>();

            if (ModelState.IsValid)
            {
                var song = db.BaiHats.Find(model.MaBaiHat);

                // Check quyền lần nữa
                if (song == null || (song.NguoiUpload != user.MaNguoiDung && user.PhanQuyen != 2))
                {
                    return RedirectToAction("Index", "Home");
                }

                // Cập nhật thông tin cơ bản
                song.TenBaiHat = model.TenBaiHat;
                song.NguoiTrinhBay = model.NguoiTrinhBay;

                // Nếu link thay đổi thì parse lại ID
                string cleanId = ExtractYoutubeID(model.LinkYoutube);
                if (!string.IsNullOrEmpty(cleanId))
                {
                    song.LinkYoutube = cleanId;
                }

                // Nếu là Nghệ sĩ sửa -> Reset trạng thái về "Chờ duyệt" để Admin kiểm tra lại
                // Nếu là Admin sửa -> Giữ nguyên hoặc cho phép duyệt luôn
                if (user.PhanQuyen != 2)
                {
                    song.TrangThaiDuyet = 0;
                }

                // --- XỬ LÝ CẬP NHẬT TAG (Phức tạp nhất) ---

                // 1. Lấy danh sách Tag cũ mà user đã vote cho bài này
                var oldVotes = db.TagVotes.Where(tv => tv.MaBaiHat == song.MaBaiHat && tv.MaNguoiDung == user.MaNguoiDung).ToList();

                // 2. Xóa các vote cho tag mà user đã BỎ chọn (Có trong DB nhưng không có trong selectedTags)
                if (selectedTags == null) selectedTags = new int[0]; // Tránh null

                foreach (var vote in oldVotes)
                {
                    if (!selectedTags.Contains(vote.MaTag))
                    {
                        db.TagVotes.Remove(vote);
                    }
                }

                // 3. Thêm các vote cho tag MỚI (Có trong selectedTags nhưng chưa có trong DB)
                foreach (var tagId in selectedTags)
                {
                    if (!oldVotes.Any(v => v.MaTag == tagId))
                    {
                        TagVote newVote = new TagVote();
                        newVote.MaBaiHat = song.MaBaiHat;
                        newVote.MaTag = tagId;
                        newVote.MaNguoiDung = user.MaNguoiDung;
                        newVote.NgayVote = DateTime.Now;
                        db.TagVotes.Add(newVote);
                    }
                }

                db.SaveChanges();
                TempData["Success"] = "Cập nhật bài hát thành công!";

                // Điều hướng về đúng trang quản lý tùy theo quyền
                if (user.PhanQuyen == 2) return RedirectToAction("Index", "Admin");
                return RedirectToAction("MySongs");
            }

            return View(model);
        }

        // 3. POST: Xóa bài hát
        [HttpPost]
        public ActionResult Delete(int id)
        {
            if (Session["User"] == null) return Json(new { success = false, msg = "Cần đăng nhập!" });
            var user = Session["User"] as NguoiDung;

            var song = db.BaiHats.Find(id);
            if (song == null) return Json(new { success = false, msg = "Không tìm thấy bài hát!" });

            // Check quyền: Chính chủ hoặc Admin
            if (song.NguoiUpload != user.MaNguoiDung && user.PhanQuyen != 2)
            {
                return Json(new { success = false, msg = "Bạn không có quyền xóa bài này!" });
            }

            // Xóa bài hát (Cascade Delete trong SQL sẽ tự xóa TagVote, Playlist_ChiTiet liên quan)
            db.BaiHats.Remove(song);
            db.SaveChanges();

            return Json(new { success = true });
        }
        // --- HELPER FUNCTIONS ---

        private string ExtractYoutubeID(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            var regex = new Regex(@"(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^""&?\/\s]{11})", RegexOptions.IgnoreCase);
            var match = regex.Match(url);
            if (match.Success) return match.Groups[1].Value;
            if (url.Length == 11) return url;
            return null;
        }

        // HÀM ĐÃ SỬA LỖI FONT UNICODE
        private string GetYoutubeChannelName(string videoId)
        {
            try
            {
                string apiUrl = $"https://www.youtube.com/oembed?url=https://www.youtube.com/watch?v={videoId}&format=json";
                using (var client = new WebClient())
                {
                    // Cấu hình Encoding để tải về đúng định dạng UTF-8
                    client.Encoding = Encoding.UTF8;
                    // Thêm User-Agent để tránh bị chặn bởi một số server
                    client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                    string json = client.DownloadString(apiUrl);

                    // SỬ DỤNG JavaScriptSerializer ĐỂ GIẢI MÃ JSON CHUẨN
                    // Cách này sẽ tự động chuyển đổi \u01a1 thành ơ
                    var serializer = new JavaScriptSerializer();
                    var data = serializer.Deserialize<Dictionary<string, object>>(json);

                    if (data != null && data.ContainsKey("author_name"))
                    {
                        return data["author_name"].ToString();
                    }
                }
            }
            catch
            {
                return null;
            }
            return null;
        }
    }
}