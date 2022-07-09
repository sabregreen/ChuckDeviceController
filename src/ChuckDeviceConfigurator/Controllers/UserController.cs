﻿namespace ChuckDeviceConfigurator.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;

    using ChuckDeviceConfigurator.Data;
    using ChuckDeviceConfigurator.ViewModels;

    [Authorize(Roles = RoleConsts.UsersRole)]
    public class UserController : Controller
    {
        private readonly ILogger<UserController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserController(
            ILogger<UserController> logger,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _logger = logger;
            _roleManager = roleManager;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var userRolesViewModel = new List<UserRolesViewModel>();
            foreach (var user in users)
            {
                var viewModel = new UserRolesViewModel
                {
                    UserId = user.Id,
                    Email = user.Email,
                    UserName = user.UserName,
                    Roles = await GetUserRoles(user),
                };
                userRolesViewModel.Add(viewModel);
            }
            return View(userRolesViewModel);
        }

        public async Task<IActionResult> Manage(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                ViewBag.ErrorMessage = $"User account with Id '{userId}' does not exist";
                return View("NotFound");
            }

            ViewBag.userId = userId;
            ViewBag.UserName = user.UserName;
            var model = new ManageUserViewModel
            {
                UserName = user.UserName,
                Email = user.Email,
            };
            foreach (var role in _roleManager.Roles)
            {
                var userRolesViewModel = new ManageUserRolesViewModel
                {
                    RoleId = role.Id,
                    RoleName = role.Name,
                    Selected = false,// TODO: Fix "This MySqlConnection is already in use" await _userManager.IsInRoleAsync(user, role.Name),
                };
                model.Roles.Add(userRolesViewModel);
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manage(ManageUserViewModel model, string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                ModelState.AddModelError("", $"User account with id '{userId}' does not exist");
                return View(model);
            }

            // Check if any existing user accounts are registered with new email that are not the existing user
            if (_userManager.Users.FirstOrDefault(user => user.Email == model.Email && user.Id != userId) != null)
            {
                ModelState.AddModelError("", $"User account with email '{model.Email}' already exists, please use a different email address.");
                return View(model);
            }

            // Only update user account password if 'Password' and 'ConfirmPassword' fields are set
            if (!string.IsNullOrEmpty(model.Password) && !string.IsNullOrEmpty(model.ConfirmPassword))
            {
                if (model.Password != model.ConfirmPassword)
                {
                    // Passwords do not match
                    ModelState.AddModelError("", $"Provided password and confirm password do not match.");
                    return View(model);
                }
                else
                {
                    // Passwords match, proceed with updating user account password by removing the exiting password used
                    await _userManager.RemovePasswordAsync(user);
                    // Add new password to user account
                    await _userManager.AddPasswordAsync(user, model.Password);
                }
            }

            // Assign new email address only if it does not match existing email address for user account
            if (user.Email != model.Email)
            {
                // TODO: Add some type of verification that the user owns the email account
                // Assign user new email address
                user.Email = model.Email;

                // Update user account email address
                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    var errors = string.Join("\n", result.Errors.Select(err => err.Description));
                    ModelState.AddModelError("", errors);
                    return View(model);
                }
            }

            // Check if any roles selected before updating user account's assigned roles
            if (model.Roles.Any(role => role.Selected))
            {
                // Get select role names
                var selectedRoles = model.Roles.Where(role => role.Selected)
                                               .Select(role => role.RoleName)
                                               .ToList();

                // Fetch all existing roles assigned to user account
                var roles = await _userManager.GetRolesAsync(user);

                // Remove all assigned roles from user
                var removeResult = await _userManager.RemoveFromRolesAsync(user, roles);
                if (!removeResult.Succeeded)
                {
                    ModelState.AddModelError("", $"Cannot remove already assigned roles from user account '{model.UserName}'");
                    return View(model);
                }

                // Assign user account new list of expected roles
                var addResult = await _userManager.AddToRolesAsync(user, selectedRoles);
                if (!addResult.Succeeded)
                {
                    ModelState.AddModelError("", $"Cannot assign selected roles to user account '{model.UserName}'");
                    return View(model);
                }
            }

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Create()
        {
            var roles = new List<ManageUserRolesViewModel>();
            foreach (var role in _roleManager.Roles)
            {
                var userRolesViewModel = new ManageUserRolesViewModel
                {
                    RoleId = role.Id,
                    RoleName = role.Name,
                    Selected = false,
                };
                roles.Add(userRolesViewModel);
            }
            var model = new CreateUserViewModel
            {
                Roles = roles,
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            try
            {
                if (_userManager.FindByNameAsync(model.UserName) != null)
                {
                    ModelState.AddModelError("User", $"User account by name '{model.UserName}' already exists, please choose a different username. It is also possible to use your email address as your username.");
                    return View(model);
                }
                if (_userManager.FindByEmailAsync(model.Email) != null)
                {
                    ModelState.AddModelError("User", $"User account with email '{model.Email}' already exists, please use a different email address.");
                    return View(model);
                }
                if (model.Password != model.ConfirmPassword)
                {
                    ModelState.AddModelError("User", $"Provided password and confirm password do not match.");
                    return View(model);
                }

                var user = new ApplicationUser
                {
                    UserName = model.UserName,
                    Email = model.Email,
                };

                var userResult = await _userManager.CreateAsync(user, model.Password);
                if (!userResult.Succeeded)
                {
                    var errors = string.Join("\n", userResult.Errors.Select(err => err.Description));
                    ModelState.AddModelError("User", errors);
                    return View(model);
                }

                async Task AssignDefaultRegisteredRole(ApplicationUser user)
                {
                    await _userManager.AddToRoleAsync(user, Roles.Registered.ToString());
                }

                // TODO: Might need to send confirmation email so user can login, since we have non-confirmed
                // accounts set unable to login unless confirmed.

                // Assign the default registered user role if no roles specified so the user can manage
                // their account at the very least until given more permissions/access by an Admin.
                if (model.Roles.Count == 0)
                {
                    await AssignDefaultRegisteredRole(user);
                }
                else
                {
                    var roleNames = model.Roles.Where(role => role.Selected).Select(role => role.RoleName);
                    var rolesResult = await _userManager.AddToRolesAsync(user, roleNames);
                    if (!rolesResult.Succeeded)
                    {
                        var errors = string.Join("\n", rolesResult.Errors.Select(err => err.Description));
                        _logger.LogError($"Failed to assign roles to user account '{model.UserName}'. Returned errors: {errors}");
                    }

                    // REVIEW: Might want to make this configurable, unsure at the moment
                    if (!await _userManager.IsInRoleAsync(user, Roles.Registered.ToString()))
                    {
                        // User not assigned default registered role, assign it
                        await AssignDefaultRegisteredRole(user);
                    }
                }

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                ModelState.AddModelError("User", $"Unknown error occurred while creating new user account.");
                return View(model);
            }
        }

        public async Task<IActionResult> Delete(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                // Failed to retrieve user account from database, does it exist?
                ModelState.AddModelError("User", $"User account does not exist with username '{userId}'.");
                return View();
            }
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(ApplicationUser user, string userId)
        {
            try
            {
                var userAccount = await _userManager.FindByIdAsync(userId);
                if (userAccount == null)
                {
                    // Failed to retrieve user account from database, does it exist?
                    ModelState.AddModelError("User", $"User account does not exist with username '{userId}'.");
                    return View(user);
                }

                // Delete user account from database
                var result = await _userManager.DeleteAsync(userAccount);
                if (!result.Succeeded)
                {
                    return View(userAccount);
                }

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                ModelState.AddModelError("User", $"Unknown error occurred while deleting user account '{userId}'.");
                return View(user);
            }
        }

        private async Task<List<string>> GetUserRoles(ApplicationUser user)
        {
            return new List<string>(await _userManager.GetRolesAsync(user));
        }
    }
}