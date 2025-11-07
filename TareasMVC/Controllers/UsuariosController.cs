using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TareasMVC.Models;
using TareasMVC.Servicios;

namespace TareasMVC.Controllers
{
    public class UsuariosController : Controller
    {
        private readonly UserManager<IdentityUser> userManager;
        private readonly SignInManager<IdentityUser> signInManager;
        private readonly ApplicationDbContext context;

        public UsuariosController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ApplicationDbContext context)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.context = context;
        }

        [AllowAnonymous]
        public IActionResult Registro()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Registro(RegistroViewModel modelo)
        {
            if (!ModelState.IsValid)
            {
                return View(modelo);
            }
            var usuario = new IdentityUser() { Email = modelo.Email, UserName = modelo.Email };
            var resultado = await userManager.CreateAsync(usuario, password: modelo.Password);
            if (resultado.Succeeded)
            {
                await signInManager.SignInAsync(usuario, isPersistent: true);
                return RedirectToAction("Index", "Home");
            }
            else
            {
                foreach (var error in resultado.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(modelo);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string mensaje = null)
        {
            if (mensaje is not null)
            {
                ViewData["mensaje"] = mensaje;
            }
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken] // recomendado: proteger contra CSRF
        public async Task<IActionResult> Login(LoginViewModel modelo)
        {
            if (!ModelState.IsValid)
            {
                return View(modelo);
            }
            var resultado = await signInManager.PasswordSignInAsync(modelo.Email, modelo.Password, modelo.Recuerdame, lockoutOnFailure: false);
            if (resultado.Succeeded)
            {
                return RedirectToAction("Index", "Home");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Nombre de usuario o password incorrecto");
                return View(modelo);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken] // recomendado: proteger contra CSRF
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [AllowAnonymous]
        public ChallengeResult LoginExterno(string proveedor, string urlRetorno = null)
        {
            // Construir la URL a la acción que procesará la respuesta del proveedor externo.
            var urlRedireccion = Url.Action("RegistrarUsuarioExterno", values: new { urlRetorno });
            // Preparar las propiedades de autenticación que se enviarán al proveedor.
            // ConfigureExternalAuthenticationProperties establece el RedirectUri y almacena
            // información adicional necesaria por el middleware (p.ej. metadatos del proveedor).
            var propiedades = signInManager.ConfigureExternalAuthenticationProperties(proveedor, urlRedireccion);
            // Retornar un ChallengeResult para que ASP.NET Core inicie el challenge contra el proveedor.
            // El middleware traducirá esto en una redirección al endpoint del proveedor (OAuth/OpenID Connect, etc.).
            return new ChallengeResult(proveedor, propiedades);
        }

        [AllowAnonymous]
        public async Task<IActionResult> RegistrarUsuarioExterno(string urlRetorno = null, string remoteError = null)
        {
            // Normalizar la URL de retorno: por defecto la raíz de la aplicación.
            urlRetorno = urlRetorno ?? Url.Content("~/");
            var mensaje = "";
            // Si el proveedor devolvió un error, redirigimos al login con el mensaje.
            if (remoteError is not null)
            {
                mensaje = $"Error del proveedor externo: {remoteError}";
                return RedirectToAction("login", routeValues: new { mensaje });
            }
            // Recuperar la información del login externo (provider, providerKey, claims, etc.).
            var info = await signInManager.GetExternalLoginInfoAsync();
            // Si es null significa que el middleware no pudo cargar la info (error en el flujo).
            if (info == null)
            {
                mensaje = "Error cargando la data de login externo";
                return RedirectToAction("login", routeValues: new { mensaje });
            }
            // Intentar iniciar sesión con la información externa.
            // - isPersistent: controla si la cookie será persistente.
            // - bypassTwoFactor: permite evitar 2FA si la cuenta ya estaba configurada para ello.
            var resultadoLoginExterno = await signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);
            // Si la cuenta ya existe y el login externo está vinculada, redirigimos al destino.
            if (resultadoLoginExterno.Succeeded)
            {
                // LocalRedirect protege contra redirecciones a URLs externas (open redirect).
                return LocalRedirect(urlRetorno);
            }
            // Caso contrario el usuario no tiene cuenta con nosotros y tenemos que crearsela
            string email = "";
            // Primero intentamos obtener el correo electrónico desde los claims del proveedor.
            if (info.Principal.HasClaim(c => c.Type == ClaimTypes.Email))
            {
                email = info.Principal.FindFirstValue(ClaimTypes.Email);
            }
            else
            {
                // Si no hay email en los claims, no podemos crear la cuenta automáticamente.
                mensaje = "Error leyendo el email del usuario del proveedor";
                return RedirectToAction("login", routeValues: new { mensaje });
            }
            // Crear el usuario local con el email recibido.
            var usuario = new IdentityUser { Email = email, UserName = email };
            var resultadoCrearUsuario = await userManager.CreateAsync(usuario);

            if (!resultadoCrearUsuario.Succeeded)
            {
                // Si falla la creación, enviamos el primer error como mensaje al login.
                mensaje = resultadoCrearUsuario.Errors.First().Description;
                return RedirectToAction("login", routeValues: new { mensaje });
            }
            // Vincular el login externo al usuario local recién creado.
            var resultadoAgregarLogin = await userManager.AddLoginAsync(usuario, info);
            if (resultadoAgregarLogin.Succeeded)
            {
                // Firmar al usuario y redirigir al retorno (ya está vinculado).
                await signInManager.SignInAsync(usuario, isPersistent: true, info.LoginProvider);
                return LocalRedirect(urlRetorno);
            }
            // Si hubo algún problema al agregar el login, notificar.
            mensaje = "Ha ocurrido un error agregando el login";
            return RedirectToAction("login", routeValues: new { mensaje });
        }

        [HttpGet]
        [Authorize(Roles = Constantes.RolAdmin)]
        public async Task<IActionResult> Listado(string mensaje)
        {
            var usuarios = await context.Users.Select(u => new UsuarioViewModel
            {
                Email = u.Email
            }).ToListAsync();

            var modelo = new UsuariosListadoViewModel();
            modelo.Usuarios = usuarios;
            modelo.Mensaje = mensaje;
            return View(modelo);
        }

        [HttpPost]
        [Authorize(Roles = Constantes.RolAdmin)]
        public async Task<IActionResult> HacerAdmin(string email)
        {
            var usuario = await context.Users.Where(u => u.Email == email).FirstOrDefaultAsync();
            if (usuario == null)
            {
                return NotFound();
            }

            await userManager.AddToRoleAsync(usuario, Constantes.RolAdmin);
            return RedirectToAction("Listado", routeValues: new { mensaje = "Rol asignado correctamente a " + email });
        }

        [HttpPost]
        [Authorize(Roles = Constantes.RolAdmin)]
        public async Task<IActionResult> RemoverAdmin(string email)
        {
            var usuario = await context.Users.Where(u => u.Email == email).FirstOrDefaultAsync();
            if (usuario == null)
            {
                return NotFound();
            }

            await userManager.RemoveFromRoleAsync(usuario, Constantes.RolAdmin);
            return RedirectToAction("Listado", routeValues: new { mensaje = "Rol removido correctamente a " + email });
        }
    }
}
