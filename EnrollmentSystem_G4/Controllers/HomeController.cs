using System;
using Microsoft.AspNetCore.Mvc;
using EnrollmentSystem_G4.Data;

namespace EnrollmentSystem_G4.Controllers
{
    public class HomeController : Controller
    {
        private readonly DatabaseHelper _db;
        private readonly ILogger<HomeController> _logger;

        public HomeController(DatabaseHelper db, ILogger<HomeController> logger)
        {
            _db = db;
            _logger = logger;
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
                _logger.LogError(ex, "Database connectivity check failed.");
                ViewBag.ConnectionStatus = "Database connection failed. Verify that MySQL is running and configured.";
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}