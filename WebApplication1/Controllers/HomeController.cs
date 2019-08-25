using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using WebApplication1.EMAIL;
using WebApplication1.MF;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    public class HomeController : Controller
    {
        //TODO: dependency injection needed
        protected Entities db;
        protected UserManager<ApplicationUser> UserManager { get; set; }
        protected ApplicationDbContext AutentificationDbContext { get; set; }
        public HomeController()
        {
            db = new Entities();
            AutentificationDbContext = new ApplicationDbContext();
            this.UserManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(AutentificationDbContext));
        }

        public ActionResult Index()
        {
            //редирект в соотвествии с ролями
            var context = HttpContext.GetOwinContext();

            if (context.Authentication.User.IsInRole("Client"))
            {
                return RedirectToAction("Client", "Home");
            }
            else if (context.Authentication.User.IsInRole("Manager"))
            {
                return RedirectToAction("Manager", "Home");
            }
            else
            {
                return RedirectToAction("Login", "Account");
            }

            //RoleManager.Create(new AppRole("Client"));
            //RoleManager.Create(new AppRole("Manager"));
        }


        [Authorize(Roles = "Client")]
        public async Task<ActionResult> Client()
        {
            try
            {
                bool returnmsg = false;
                int hours = 0;
                //TODO: выяснить есть ли готовый асинхр. метод
                await Task.Run(() =>
                {
                    //время последнего сообщения
                    //в настоящем проекте это должна быть отдельная таблица, с отметкой о последнем сообщении клиента, т.к. OrderBy может занимать время, если сообщений много
                    DateTime lastDate = db.Posts.Where(n => n.Name == User.Identity.Name).OrderByDescending(k => k.Date).FirstOrDefault().Date;
                    TimeSpan diff = DateTime.Now - lastDate;
                    hours = (int)diff.TotalHours;
                    if (hours < 24) returnmsg = true;
                });
                if (returnmsg)
                {
                    ViewBag.Msg = "Сообщения отправляются не чаще 1 раза в сутки, зайдите через " + (24 - hours).ToString() + " ч.";
                    return View("ClientTime");
                }
            }
            catch
            {
                //TODO: придумать обработку
            }

            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Client")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Client(Posts model, string returnUrl, HttpPostedFileBase upload)
        {
            //TODO: сделать валидацию на клиенте

            //сообщение должно содержать либо файл, либо текст, либо все вместе            
            bool valid = false;

            if (upload != null)
            {
                await Task.Run(() =>
                {
                    //файлы на диске будут с уникальными именами
                    string guidFileName = Guid.NewGuid().ToString();
                    upload.SaveAs(Server.MapPath("~/Files/" + guidFileName));
                    model.File = upload.FileName; //настоящее имя файла тоже сохраняется
                    model.FileName = guidFileName;
                    valid = true;
                });

            }

            //
            if (!String.IsNullOrEmpty(model.Text)) valid = true;

            if (valid)
            {
                model.Date = DateTime.Now;
                model.Status = 0;
                model.Name = User.Identity.Name;
                model.UserID = User.Identity.GetUserId();
                var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                model.Email = user.Email;
                db.Posts.Add(model);
                await db.SaveChangesAsync();
                return RedirectToAction("Client");
            }

            if (!valid) ViewBag.Msg = "Сообщение должно содержать либо файл, либо текст, либо файл и текст.";

            //отправка почты. 
            if (valid)
            {
                string EmailBody = $"{model.Date}: Клиент {model.Name} (mailto:{model.Email}) пишет: " +
                                                                Environment.NewLine + model.Text;
                //там сразу return т.к. для реальной отправки нужен ящик: пароль, smtp и т.д.
                await MEmail.Send(EmailBody);
            }

            return View();
        }

        [Authorize(Roles = "Manager")]
        public async Task<ActionResult> Manager()
        {
            var posts = await db.Posts.Where(c => c.Status == 0).ToListAsync();
            return View(posts);
        }

        [Authorize(Roles = "Manager")]
        public ActionResult Download(string name, string file)
        {
            //TODO: выяснить можно ли и нужно ли асинхронно
            return File(file, "other", name);
        }

        //private AppRoleManager RoleManager
        //{
        //    get
        //    {
        //        return HttpContext.GetOwinContext().GetUserManager<AppRoleManager>();
        //    }
        //}

    }
}