document.addEventListener("click", function (event) {
    const button = event.target.closest(".embed-button");
    if (!button || button.disabled) return;
    const target = button.nextElementSibling;
    const publicUrl = button.dataset.url;
    button.disabled = true;
    button.textContent = "Loading public feed…";
    if (button.dataset.provider === "facebook") {
        const frame = document.createElement("iframe");
        frame.title = "Public Facebook content";
        frame.loading = "lazy";
        frame.referrerPolicy = "no-referrer";
        frame.src = "https://www.facebook.com/plugins/page.php?href=" + encodeURIComponent(publicUrl) + "&tabs=timeline&width=340&height=500&small_header=true&adapt_container_width=true";
        target.appendChild(frame);
    } else {
        const anchor = document.createElement("a");
        anchor.className = "twitter-timeline";
        anchor.href = publicUrl;
        anchor.textContent = "Posts from this public X profile";
        target.appendChild(anchor);
        if (!document.getElementById("x-wjs")) {
            const script = document.createElement("script");
            script.id = "x-wjs";
            script.src = "https://platform.twitter.com/widgets.js";
            script.async = true;
            document.body.appendChild(script);
        } else if (window.twttr?.widgets) window.twttr.widgets.load(target);
    }
});
