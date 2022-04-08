using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OAuth.Homework.Models;

namespace OAuth.Homework.Controllers
{
    public class SubscribersController : Controller
    {
        private readonly SubscriberContext _context;

        public SubscribersController(SubscriberContext context)
        {
            _context = context;
        }

        // GET: Subscribers
        [Authorize("AdminsOnly")]
        public async Task<IActionResult> Index()
        {
            return View(await _context.Subscribers.ToListAsync());
        }
    }
}
