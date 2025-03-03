// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
Swal.fire({
    icon: 'success',
    title: 'Success',
    text: '@TempData["SuccessMessage"]',
    confirmButtonColor: '#28a745',
    confirmButtonText: 'Great!',
    timer: 3000,
    timerProgressBar: true,
    showClass: {
        popup: 'animate__animated animate__fadeInDown'
    },
    hideClass: {
        popup: 'animate__animated animate__fadeOutUp'
    }
});