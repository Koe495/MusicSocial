using QLAmNhac.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace QLAmNhac.Controllers
{
    public class PlaylistController : Controller
    {
        // Sử dụng đúng DbContext của bạn
        private QLAmNhac_Web1Entities1 db = new QLAmNhac_Web1Entities1();

        // 1. Danh sách Playlist của tôi (Trang quản lý riêng)
        public ActionResult Index(string q)
        {
            if (Session["User"] == null) return RedirectToAction("Login", "Account");
            var user = Session["User"] as NguoiDung;

            var playlistsQuery = db.Playlists.Where(p => p.MaNguoiDung == user.MaNguoiDung);

            if (!string.IsNullOrWhiteSpace(q))
            {
                string key = q.Trim();
                playlistsQuery = playlistsQuery.Where(p => p.TenPlaylist.Contains(key));
            }

            var playlists = playlistsQuery.OrderByDescending(p => p.NgayTao).ToList();
            ViewBag.Query = q;
            return View(playlists);
        }

        // 2. Tạo Playlist mới
        [HttpPost]
        public ActionResult CreateAjax(string tenPlaylist)
        {
            if (Session["User"] == null) return Json(new { success = false, msg = "Bạn cần đăng nhập!" });
            if (string.IsNullOrWhiteSpace(tenPlaylist)) return Json(new { success = false, msg = "Tên Playlist không được để trống!" });

            try
            {
                var user = Session["User"] as NguoiDung;

                var pl = new Playlist();
                pl.TenPlaylist = tenPlaylist;
                pl.MaNguoiDung = user.MaNguoiDung;
                pl.CheDoRiengTu = false; // Mặc định là công khai
                pl.NgayTao = DateTime.Now;

                db.Playlists.Add(pl);
                db.SaveChanges();

                return Json(new { success = true, msg = "Tạo playlist thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = "Lỗi server: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult Create(string name, bool isPrivate)
        {
            if (Session["User"] == null) return Json(new { success = false, msg = "Bạn cần đăng nhập!" });
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, msg = "Tên Playlist không được để trống!" });

            try
            {
                var user = Session["User"] as NguoiDung;

                var pl = new Playlist();
                pl.TenPlaylist = name.Trim();
                pl.MaNguoiDung = user.MaNguoiDung;
                pl.CheDoRiengTu = isPrivate;
                pl.NgayTao = DateTime.Now;

                db.Playlists.Add(pl);
                db.SaveChanges();

                return Json(new { success = true, msg = "Tạo playlist thành công!", id = pl.MaPlaylist, name = pl.TenPlaylist });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = "Lỗi server: " + ex.Message });
            }
        }

        // 3. Xem chi tiết Playlist
        public ActionResult Details(int id)
        {
            var pl = db.Playlists.Find(id);
            if (pl == null) return HttpNotFound();

            // Logic kiểm tra quyền riêng tư
            if (pl.CheDoRiengTu == true)
            {
                if (Session["User"] == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var user = Session["User"] as NguoiDung;
                if (pl.MaNguoiDung != user.MaNguoiDung)
                {
                    // Nếu là Private và không phải chủ sở hữu -> Về trang chủ
                    return RedirectToAction("Index", "Home");
                }
            }

            return View(pl);
        }

        // 4. Thêm bài hát vào Playlist (AJAX)
        [HttpPost]
        public ActionResult AddSong(int playlistId, int songId)
        {
            if (Session["User"] == null) return Json(new { success = false, msg = "Bạn cần đăng nhập!" });
            var user = Session["User"] as NguoiDung;

            var pl = db.Playlists.Find(playlistId);

            // Kiểm tra quyền sở hữu
            if (pl == null) return Json(new { success = false, msg = "Playlist không tồn tại!" });
            if (pl.MaNguoiDung != user.MaNguoiDung) return Json(new { success = false, msg = "Bạn không có quyền sửa playlist này!" });

            // Kiểm tra trùng bài hát
            if (db.Playlist_ChiTiet.Any(x => x.MaPlaylist == playlistId && x.MaBaiHat == songId))
                return Json(new { success = false, msg = "Bài hát này đã có trong playlist rồi!" });

            try
            {
                // Tính thứ tự mới: Lấy max thứ tự hiện tại + 1
                // Sử dụng cú pháp an toàn cho Nullable int
                int maxOrder = 0;
                var existingItems = db.Playlist_ChiTiet.Where(x => x.MaPlaylist == playlistId);
                if (existingItems.Any())
                {
                    maxOrder = existingItems.Max(x => x.ThuTu) ?? 0;
                }

                var chiTiet = new Playlist_ChiTiet();
                chiTiet.MaPlaylist = playlistId;
                chiTiet.MaBaiHat = songId;
                // chiTiet.NgayThem = DateTime.Now; // Mở comment nếu DB có cột NgayThem
                chiTiet.ThuTu = maxOrder + 1;

                db.Playlist_ChiTiet.Add(chiTiet);
                db.SaveChanges();

                return Json(new { success = true, msg = "Đã thêm vào playlist: " + pl.TenPlaylist });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = "Lỗi: " + ex.Message });
            }
        }

        // 5. Xóa bài hát khỏi Playlist (AJAX)
        [HttpPost]
        public ActionResult RemoveSong(int playlistId, int songId)
        {
            if (Session["User"] == null) return Json(new { success = false, msg = "Cần đăng nhập!" });
            var user = Session["User"] as NguoiDung;

            var pl = db.Playlists.Find(playlistId);
            if (pl == null || pl.MaNguoiDung != user.MaNguoiDung)
                return Json(new { success = false, msg = "Bạn không có quyền!" });

            var item = db.Playlist_ChiTiet.FirstOrDefault(x => x.MaPlaylist == playlistId && x.MaBaiHat == songId);
            if (item != null)
            {
                db.Playlist_ChiTiet.Remove(item);
                db.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false, msg = "Không tìm thấy bài hát trong playlist" });
        }

        // 6. Xóa Playlist (AJAX)
        [HttpPost]
        public ActionResult Delete(int id)
        {
            if (Session["User"] == null) return Json(new { success = false, msg = "Cần đăng nhập!" });
            var user = Session["User"] as NguoiDung;

            var pl = db.Playlists.Find(id);
            if (pl == null || pl.MaNguoiDung != user.MaNguoiDung)
                return Json(new { success = false, msg = "Bạn không có quyền xóa!" });

            try
            {
                // Xóa chi tiết trước (nếu DB không thiết lập Cascade Delete)
                var chiTiets = db.Playlist_ChiTiet.Where(x => x.MaPlaylist == id).ToList();
                db.Playlist_ChiTiet.RemoveRange(chiTiets);

                db.Playlists.Remove(pl);
                db.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = "Lỗi khi xóa: " + ex.Message });
            }
        }

        // 7. API lấy danh sách Playlist nhỏ gọn (cho Modal thêm nhạc)
        [HttpPost]
        public ActionResult GetMyPlaylists()
        {
            if (Session["User"] == null) return Json(new { success = false, msg = "Chưa đăng nhập" });
            var user = Session["User"] as NguoiDung;

            try
            {
                var list = db.Playlists
                             .Where(p => p.MaNguoiDung == user.MaNguoiDung)
                             .OrderByDescending(p => p.MaPlaylist) // Mới nhất lên đầu
                             .Select(p => new {
                                 MaPlaylist = p.MaPlaylist,
                                 TenPlaylist = p.TenPlaylist,
                                 SoLuong = p.Playlist_ChiTiet.Count
                             }).ToList();

                return Json(new { success = true, data = list });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = ex.Message });
            }
        }

        // 8. Cập nhật thứ tự bài hát (AJAX - Cho tính năng Kéo Thả)
        [HttpPost]
        public ActionResult UpdateOrder(int playlistId, int[] songIds)
        {
            if (Session["User"] == null) return Json(new { success = false, msg = "Chưa đăng nhập" });

            // Kiểm tra quyền sở hữu playlist trước khi xếp lại
            var user = Session["User"] as NguoiDung;
            var pl = db.Playlists.Find(playlistId);
            if (pl == null || pl.MaNguoiDung != user.MaNguoiDung)
                return Json(new { success = false, msg = "Không có quyền" });

            if (songIds != null && songIds.Length > 0)
            {
                var songs = db.Playlist_ChiTiet.Where(x => x.MaPlaylist == playlistId).ToList();

                for (int i = 0; i < songIds.Length; i++)
                {
                    var songId = songIds[i];
                    var item = songs.FirstOrDefault(s => s.MaBaiHat == songId);
                    if (item != null)
                    {
                        item.ThuTu = i + 1; // Cập nhật thứ tự: 1, 2, 3...
                    }
                }
                db.SaveChanges();
            }

            return Json(new { success = true });
        }

        // Giải phóng tài nguyên
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}