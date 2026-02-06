mergeInto(LibraryManager.library, {

  ShowTurnstile: function (siteKeyPtr) {
    try {
      var siteKey = UTF8ToString(siteKeyPtr);
      console.log("[Turnstile] ShowTurnstile called with key: " + siteKey);

      // Check if Turnstile is loaded
      if (typeof turnstile === 'undefined') {
        console.error("[Turnstile] Turnstile script not loaded!");
        return;
      }

      // Check if token input exists (created by Index.html template)
      var tokenInput = document.getElementById("cf-turnstile-response");
      if (!tokenInput) {
         // Create hidden input if not exists (fallback)
         tokenInput = document.createElement("input");
         tokenInput.type = "hidden";
         tokenInput.id = "cf-turnstile-response";
         document.body.appendChild(tokenInput);
      }

      var widgetId = "turnstile-widget";
      var container = document.getElementById(widgetId);
      
      if (!container) {
          // Create container if not exists (overlay style)
          container = document.createElement("div");
          container.id = widgetId;
          container.style.position = "absolute";
          container.style.top = "50%";
          container.style.left = "50%";
          container.style.transform = "translate(-50%, -50%)";
          container.style.zIndex = "9999";
          container.style.backgroundColor = "white";
          container.style.padding = "20px";
          container.style.borderRadius = "8px";
          container.style.boxShadow = "0 0 10px rgba(0,0,0,0.5)";
          // Simple close button/text
          container.innerHTML = "<div style='margin-bottom:10px;text-align:center;'>Security Check</div>";
          document.body.appendChild(container);
      }

      // Render Turnstile
      turnstile.render("#" + widgetId, {
        sitekey: siteKey,
        callback: function(token) {
          console.log("[Turnstile] Token received: " + token);
          var input = document.getElementById("cf-turnstile-response");
          if(input) input.value = token;
          
          // Optionally notify Unity directly here if needed
          // MyGameInstance.SendMessage('SocketClient', 'OnTurnstileSuccess', token); 
          // But our design uses Polling or GetToken before sending.
        },
        "error-callback": function() {
           console.error("[Turnstile] Error!");
        }
      });

    } catch (e) {
      console.error("[Turnstile] Error in ShowTurnstile: " + e);
    }
  },

  GetTurnstileToken: function () {
    var input = document.getElementById("cf-turnstile-response");
    var token = (input && input.value) ? input.value : "";
    var bufferSize = lengthBytesUTF8(token) + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(token, buffer, bufferSize);
    return buffer;
  },

  ResetTurnstile: function() {
     if (typeof turnstile !== 'undefined') turnstile.reset();
     var input = document.getElementById("cf-turnstile-response");
     if(input) input.value = "";
  }

});
