using QLAmNhac.Models;
using System;
using System.Linq;
using System.Web.Mvc;

namespace QLAmNhac.Controllers
{
    public class AdminController : Controller
    {
        private QLAmNhac_Web1Entities1 db = new QLAmNhac_Web1Entities1();

        // 1. DUYỆT BÀI
        public ActionResult Index()
        {
            if (Session["User"] == null) return RedirectToAction("Login", "Account");
            var user = Session["User"] as NguoiDung;
            if (user.PhanQuyen != 2) return RedirectToAction("Index", "Home");

            var pendingSongs = db.BaiHats.Where(s => s.TrangThaiDuyet == 0).OrderBy(s => s.NgayDang).ToList();
            return View(pendingSongs);
        }

        // 2. QUẢN LÝ NGHỆ SĨ (Danh sách các Artist)
        public ActionResult ArtistManager()
        {
            if (Session["User"] == null) return RedirectToAction("Login", "Account");
            var user = Session["User"] as NguoiDung;
            if (user.PhanQuyen != 2) return RedirectToAction("Index", "Home");

            // Lấy tất cả user có PhanQuyen = 1 (Nghệ sĩ)
            var artists = db.NguoiDungs.Where(u => u.PhanQuyen == 1).ToList();
            return View(artists);
        }

        // User manager - list all non-admin users for role change / deletion
        // Supports search (q) and role filter (role: 0 = user, 1 = artist)
        public ActionResult UserManager(string q, int? role)
        {
            if (Session["User"] == null) return RedirectToAction("Login", "Account");
            var admin = Session["User"] as NguoiDung;
            if (admin.PhanQuyen != 2) return RedirectToAction("Index", "Home");

            var usersQuery = db.NguoiDungs.Where(u => u.PhanQuyen != 2);

            if (!string.IsNullOrWhiteSpace(q))
            {
                string key = q.Trim();
                usersQuery = usersQuery.Where(u => u.HoTen.Contains(key) || u.TenDangNhap.Contains(key));
            }

            if (role.HasValue)
            {
                // Only allow 0 or 1 filters; ignore invalid values
                if (role.Value == 0 || role.Value == 1)
                {
                    usersQuery = usersQuery.Where(u => u.PhanQuyen == role.Value);
                }
            }

            var users = usersQuery.OrderByDescending(u => u.NgayTao).ToList();

            ViewBag.Query = q;
            ViewBag.Role = role;

            return View(users);
        }

        // 3. XEM NHẠC CỦA NGHỆ SĨ (Chi tiết nhạc của 1 người)
        public ActionResult ArtistSongs(int id)
        {
            if (Session["User"] == null) return RedirectToAction("Login", "Account");
            var user = Session["User"] as NguoiDung;
            if (user.PhanQuyen != 2) return RedirectToAction("Index", "Home");

            // Lấy thông tin nghệ sĩ để hiển thị tên
            var artist = db.NguoiDungs.Find(id);
            if (artist == null) return HttpNotFound();
            ViewBag.ArtistName = artist.HoTen;

            // Lấy danh sách nhạc của họ
            var songs = db.BaiHats.Where(s => s.NguoiUpload == id).OrderByDescending(s => s.NgayDang).ToList();
            return View(songs);
        }

        // --- CÁC HÀM XỬ LÝ DUYỆT ---
        [HttpPost]
        public ActionResult Approve(int id)
        {
            if (Session["User"] == null) return Json(new { success = false });
            var user = Session["User"] as NguoiDung;
            if (user.PhanQuyen != 2) return Json(new { success = false });

            var song = db.BaiHats.Find(id);
            if (song != null)
            {
                song.TrangThaiDuyet = 1;
                song.GhiChuAdmin = $"Duyệt bởi {user.HoTen}";
                db.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        [HttpPost]
        public ActionResult Reject(int id, string reason)
        {
            if (Session["User"] == null) return Json(new { success = false });
            var user = Session["User"] as NguoiDung;
            if (user.PhanQuyen != 2) return Json(new { success = false });

            var song = db.BaiHats.Find(id);
            if (song != null)
            {
                song.TrangThaiDuyet = -1;
                song.GhiChuAdmin = reason;
                db.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        // Thay đổi vai trò người dùng (User <-> Artist)
        [HttpPost]
        public ActionResult ToggleRole(int id)
        {
            if (Session["User"] == null) return Json(new { success = false, msg = "Cần đăng nhập!" });
            var admin = Session["User"] as NguoiDung;
            if (admin.PhanQuyen != 2) return Json(new { success = false, msg = "Không có quyền thực hiện hành động này!" });

            var target = db.NguoiDungs.Find(id);
            if (target == null) return Json(new { success = false, msg = "Không tìm thấy người dùng!" });

            // Không cho phép đổi quyền Admin
            if (target.PhanQuyen == 2) return Json(new { success = false, msg = "Không thể thay đổi vai trò của Admin!" });

            // Toggle: 0 -> 1, 1 -> 0
            target.PhanQuyen = target.PhanQuyen == 1 ? 0 : 1;
            db.SaveChanges();

            return Json(new { success = true, newRole = target.PhanQuyen });
        }

        // --- NEW: Xóa tài khoản người dùng
        [HttpPost]
        public ActionResult DeleteUser(int id)
        {
            if (Session["User"] == null) return Json(new { success = false, msg = "Cần đăng nhập!" });
            var admin = Session["User"] as NguoiDung;
            if (admin.PhanQuyen != 2) return Json(new { success = false, msg = "Không có quyền thực hiện hành động này!" });

            // Không cho admin tự xóa chính mình
            if (admin.MaNguoiDung == id) return Json(new { success = false, msg = "Bạn không thể xóa chính tài khoản mình!" });

            var target = db.NguoiDungs.Find(id);
            if (target == null) return Json(new { success = false, msg = "Không tìm thấy người dùng!" });

            if (target.PhanQuyen == 2) return Json(new { success = false, msg = "Không thể xóa tài khoản Admin!" });

            try
            {
                // 1. Xóa bình luận do user tạo
                var comments = db.BinhLuans.Where(b => b.MaNguoiDung == id).ToList();
                if (comments.Any()) db.BinhLuans.RemoveRange(comments);

                // 2. Xóa vote bình luận do user tạo
                var blVotes = db.BinhLuanVotes.Where(v => v.MaNguoiDung == id).ToList();
                if (blVotes.Any()) db.BinhLuanVotes.RemoveRange(blVotes);

                // 3. Xóa tag votes do user tạo
                var tagVotesByUser = db.TagVotes.Where(tv => tv.MaNguoiDung == id).ToList();
                if (tagVotesByUser.Any()) db.TagVotes.RemoveRange(tagVotesByUser);

                // 4. Xóa playlist và chi tiết playlist do user tạo
                var playlists = db.Playlists.Where(p => p.MaNguoiDung == id).ToList();
                foreach (var pl in playlists)
                {
                    var plDetails = db.Playlist_ChiTiet.Where(pc => pc.MaPlaylist == pl.MaPlaylist).ToList();
                    if (plDetails.Any()) db.Playlist_ChiTiet.RemoveRange(plDetails);
                }
                if (playlists.Any()) db.Playlists.RemoveRange(playlists);

                // 5. Xóa bài hát do user upload và dữ liệu liên quan
                var songs = db.BaiHats.Where(s => s.NguoiUpload == id).ToList();
                foreach (var s in songs)
                {
                    var tagVotesForSong = db.TagVotes.Where(tv => tv.MaBaiHat == s.MaBaiHat).ToList();
                    if (tagVotesForSong.Any()) db.TagVotes.RemoveRange(tagVotesForSong);

                    var playlistDetailsForSong = db.Playlist_ChiTiet.Where(pc => pc.MaBaiHat == s.MaBaiHat).ToList();
                    if (playlistDetailsForSong.Any()) db.Playlist_ChiTiet.RemoveRange(playlistDetailsForSong);

                    var commentsOnSong = db.BinhLuans.Where(b => b.MaBaiHat == s.MaBaiHat).ToList();
                    if (commentsOnSong.Any()) db.BinhLuans.RemoveRange(commentsOnSong);
                }
                if (songs.Any()) db.BaiHats.RemoveRange(songs);

                // 6. Xóa theo dõi liên quan (cả người theo và người được theo)
                var follows = db.TheoDois.Where(t => t.MaNguoiTheoDoi == id || t.MaNgheSi == id).ToList();
                if (follows.Any()) db.TheoDois.RemoveRange(follows);

                // 7. Cuối cùng xóa người dùng
                db.NguoiDungs.Remove(target);

                db.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Trả về lỗi để debug; trong production nên log chi tiết và trả thông báo chung
                return Json(new { success = false, msg = "Lỗi khi xóa: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult DeleteTagFromSong(int songId, int tagId)
        {
            if (Session["User"] == null) return Json(new { success = false, msg = "Cần đăng nhập!" });
            var admin = Session["User"] as NguoiDung;
            if (admin == null || admin.PhanQuyen != 2) return Json(new { success = false, msg = "Không có quyền thực hiện hành động này!" });

            try
            {
                // Validate song
                var song = db.BaiHats.Find(songId);
                if (song == null) return Json(new { success = false, msg = "Không tìm thấy bài hát." });

                // Validate tag
                var tag = db.Tags.Find(tagId);
                if (tag == null) return Json(new { success = false, msg = "Không tìm thấy thẻ (Tag)." });

                // Find votes linking this tag to the song
                var votes = db.TagVotes.Where(tv => tv.MaBaiHat == songId && tv.MaTag == tagId).ToList();
                if (!votes.Any())
                {
                    return Json(new { success = false, msg = "Thẻ này không tồn tại trên bài hát." });
                }

                // Remove the TagVote records for that song/tag
                db.TagVotes.RemoveRange(votes);
                db.SaveChanges();

                // Optionally: remove the Tag entry entirely if it is no longer used anywhere
                bool isTagStillUsed = db.TagVotes.Any(v => v.MaTag == tagId);
                if (!isTagStillUsed)
                {
                    // Only remove user-created tags (LaGoiY == false) to avoid deleting system suggestion tags
                    if (tag.LaGoiY == false)
                    {
                        db.Tags.Remove(tag);
                        db.SaveChanges();
                    }
                }

                return Json(new { success = true, msg = "Đã xóa thẻ khỏi bài hát." });
            }
            catch (Exception ex)
            {
                // Return message for debugging; in production you may want to log and return a generic message
                return Json(new { success = false, msg = "Lỗi server khi xóa thẻ: " + ex.Message });
            }
        }
    }
}