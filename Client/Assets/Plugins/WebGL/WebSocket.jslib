mergeInto(LibraryManager.library, {
  WebSocketConnect: function (url) {
    var urlStr = UTF8ToString(url);
    var socket = new WebSocket(urlStr);
    socket.binaryType = 'arraybuffer';

    socket.onopen = function () {
      SendMessage('SocketClient', 'OnWebSocketOpen');
    };

    socket.onmessage = function (e) {
      if (e.data instanceof ArrayBuffer) {
        var data = new Uint8Array(e.data);
        var buffer = _malloc(data.length);
        writeArrayToMemory(data, buffer);
        SendMessage('SocketClient', 'OnWebSocketMessage', JSON.stringify({ ptr: buffer, length: data.length }));
      } else {
        // Text handling if needed
        SendMessage('SocketClient', 'OnWebSocketMessageText', e.data);
      }
    };

    socket.onclose = function (e) {
        SendMessage('SocketClient', 'OnWebSocketClose', e.code);
    };

    socket.onerror = function (e) {
        SendMessage('SocketClient', 'OnWebSocketError', "Error");
    };

    window.webSocketInstance = socket;
  },

  WebSocketSend: function (ptr, length) {
    if (window.webSocketInstance && window.webSocketInstance.readyState === 1) {
      var data = HEAPU8.subarray(ptr, ptr + length);
      window.webSocketInstance.send(data);
    }
  },

  WebSocketClose: function () {
    if (window.webSocketInstance) {
      window.webSocketInstance.close();
      window.webSocketInstance = null;
    }
  },
  
  WebSocketState: function() {
    if (window.webSocketInstance) return window.webSocketInstance.readyState;
    return 3; // CLOSED
  }
});
