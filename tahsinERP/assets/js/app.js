document.addEventListener('DOMContentLoaded', () => {
    const popup = document.getElementById('pop-up');
    const openButtons = document.querySelectorAll('.open-modal');
    const closeButton = document.getElementById('pop-up__close');
    const confirmButton = document.getElementById('pop-up__action');
    const successBtn = document.querySelector(".success-btn")
    let formToSubmit = null;

    // Elementlar mavjudligini tekshirish
    if (!popup) {
        return; // Agar popup mavjud bo'lmasa, script'ni to'xtatish
    }

    function openPopup() {
        if (popup) {
            popup.classList.add('show');
        }
    }

    function closePopup() {
        if (popup) {
            popup.classList.remove('show');
        }
    }

    if (openButtons && openButtons.length > 0) {
        openButtons.forEach(button => {
            button.addEventListener('click', (event) => {
                event.preventDefault();
                formToSubmit = button.closest('form');
                openPopup();
            });
        });
    }

    if (closeButton) {
        closeButton.addEventListener('click', closePopup);
    }
    
    if (confirmButton) {
        confirmButton.addEventListener('click', () => {
            if (formToSubmit) {
                formToSubmit.submit();
            }
            closePopup();
        });
    }
});
