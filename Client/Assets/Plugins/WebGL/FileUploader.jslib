mergeInto(LibraryManager.library, {

  UploadFile: function(gameObjectName, methodName, filter) {
    var gameObjectNameStr = UTF8ToString(gameObjectName);
    var methodNameStr = UTF8ToString(methodName);
    var filterStr = UTF8ToString(filter);

    // Create a hidden file input
    var fileInput = document.createElement('input');
    fileInput.setAttribute('type', 'file');
    fileInput.setAttribute('accept', filterStr);
    fileInput.style.display = 'none';

    fileInput.onclick = function (event) {
      this.value = null;
    };

    fileInput.onchange = function (event) {
      if (this.files && this.files[0]) {
        var reader = new FileReader();
        reader.onload = function (e) {
          var content = e.target.result;
          // Send content back to Unity
          // Note: SendMessage(objectName, methodName, info)
          SendMessage(gameObjectNameStr, methodNameStr, content);
        };
        // Read as text
        reader.readAsText(this.files[0]);
      }
      // Remove input after use
      document.body.removeChild(fileInput);
    };

    document.body.appendChild(fileInput);
    fileInput.click();
  }
});
