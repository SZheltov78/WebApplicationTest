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
        protected PostsDbContext db;
        protected UserManager<ApplicationUser> UserManager { get; set; }
        protected ApplicationDbContext AutentificationDbContext { get; set; }
        public HomeController()
        {
            db = new PostsDbContext();
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

            bool returnDenied = false;
            int hours = 0;
            //TODO: выяснить есть ли готовый асинхр. метод
            await Task.Run(() =>
            {
                //время последнего сообщения
                //в настоящем проекте это должна быть отдельная таблица, с отметкой о последнем сообщении клиента, т.к. OrderBy может занимать время, если сообщений много
                try
                {
                    DateTime lastDate = db.Posts.Where(n => n.Name == User.Identity.Name).OrderByDescending(k => k.Date).FirstOrDefault().Date;
                    TimeSpan diff = DateTime.Now - lastDate;
                    hours = (int)diff.TotalHours;
                    if (hours < 24) returnDenied = true;
                }
                catch { }
            });
            if (returnDenied)
            {
                ViewBag.Msg = $"Сообщения отправляются не чаще 1 раза в сутки, зайдите через {(24 - hours).ToString()} ч.";
                return View("ClientTime");
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
                try
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
                catch (Exception ex)
                {
                    ViewBag.Msg = $"Ошибка загрузки файла: {ex.Message}";
                    return View("Error");
                }
            }

            //
            if (!String.IsNullOrEmpty(model.Text)) valid = true;

            //добавить сообщение в бд
            if (valid)
            {
                try
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
                catch (Exception ex)
                {
                    ViewBag.Msg = $"Ошибка при записи в БД: {ex.Message}";
                    return View("Error");
                }

            }

            if (!valid) ViewBag.Msg = "Сообщение должно содержать либо файл, либо текст, либо файл и текст.";

            //отправка почты. 
            if (valid)
            {
                try
                {
                    string EmailBody = $"{model.Date} Клиент {model.Name} (mailto:{model.Email}) пишет: " +
                                                                    Environment.NewLine + model.Text;
                    //там сразу return т.к. для реальной отправки нужен ящик: пароль, smtp и т.д.
                    await MEmail.Send(EmailBody);
                }
                catch (Exception ex)
                {
                    //TODO: кого оповещать в случае невозможности отправки писем менеджеру
                }
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


        //
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult> ManagerMark(string id)
        {
            try
            {
                int _id = Convert.ToInt32(id);
                Posts p = db.Posts.Where(i => i.Id == _id).First();
                p.Status = 1;
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                ViewBag.Msg = $"Ошибка при действии 'Отметить как прочитанное': {ex.Message}";
                return View("Error");
            }
            return RedirectToAction("Manager");
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