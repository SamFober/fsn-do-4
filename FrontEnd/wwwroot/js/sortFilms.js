

// // Get all the slides
// const slides = document.querySelectorAll('.slide');
// let activeIndex = 0; // Start with the first slide

// // Function to change the active slide
// function changeSlide() {
//     // Remove 'active' class from all slides
//     slides.forEach((slide) => {
//         slide.classList.remove('active');
//     });

//     // Add 'active' class to the current slide
//     slides[activeIndex].classList.add('active');

//     // Move to the next slide
//     activeIndex = (activeIndex + 1) % slides.length; // Loop back to the first slide
// }

// // Set the interval for changing the slides (every 5 seconds)
// setInterval(changeSlide, 5000); // Change slide every 5 seconds

// // Initial call to show the first slide
// changeSlide();