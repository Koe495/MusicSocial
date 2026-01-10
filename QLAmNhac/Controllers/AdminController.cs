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
    }
}