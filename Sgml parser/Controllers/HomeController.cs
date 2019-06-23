using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Sgml_parser.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> logger;
        private readonly SgmlConverter converter;

        public HomeController(ILogger<HomeController> logger, SgmlConverter converter)
        {
            this.logger = logger;
            this.converter = converter;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> Index(IFormFile inputFile)
        {
            try
            {
                string[] attachmemtTags = { "TEXT" };
                return Content(converter.ConvertToJson(inputFile.OpenReadStream(), attachmemtTags)) ;
            }
            catch (Exception exc)
            {
                logger.LogError(exc, "Error");
                return Content("Error on converting. See more details in log");
            }


        }


    }
}