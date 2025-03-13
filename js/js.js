// Function to sort films based on time
document.addEventListener("DOMContentLoaded", function() {
    document.getElementById("sort-btn").addEventListener("click", function() {
        let grid = document.querySelector(".film-grid");
        let films = Array.from(grid.getElementsByClassName("film-item"));

        films.sort((a, b) => {
            let timeA = a.querySelector(".time-slot-btn").innerText;
            let timeB = b.querySelector(".time-slot-btn").innerText;
            return timeA.localeCompare(timeB);
        });

        films.forEach(film => grid.appendChild(film));
    });
});


// Get all the slides
const slides = document.querySelectorAll('.slide');
let activeIndex = 0; // Start with the first slide

// Function to change the active slide
function changeSlide() {
    // Remove 'active' class from all slides
    slides.forEach((slide) => {
        slide.classList.remove('active');
    });

    // Add 'active' class to the current slide
    slides[activeIndex].classList.add('active');

    // Move to the next slide
    activeIndex = (activeIndex + 1) % slides.length; // Loop back to the first slide
}

// Set the interval for changing the slides (every 5 seconds)
setInterval(changeSlide, 5000); // Change slide every 5 seconds

// Initial call to show the first slide
changeSlide();