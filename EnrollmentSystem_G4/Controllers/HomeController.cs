using System;
using Microsoft.AspNetCore.Mvc;
using EnrollmentSystem_G4.Data;

namespace EnrollmentSystem_G4.Controllers
{
    public class HomeController : Controller
    {
        private readonly DatabaseHelper _db;

        public HomeController(DatabaseHelper db)
        {
            _db = db;
        }

        public IActionResult Index()
        {
            try
            {
                // Test connectivity using ExecuteScalar
                object? result = _db.ExecuteScalar("SELECT 1;");
                if (result != null)
                {
                    ViewBag.ConnectionStatus = "Database Connection Successful!";
                }
            }
            catch (Exception ex)
            {
                ViewBag.ConnectionStatus = $"Database Connection Failed: {ex.Message}";
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}