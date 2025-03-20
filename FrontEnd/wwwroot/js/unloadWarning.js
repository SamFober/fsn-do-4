window.addUnloadWarning = function () {
    console.log("Unload warning function is initialized.");
    window.addEventListener("beforeunload", function (event) {
        event.preventDefault();
    });
}; 