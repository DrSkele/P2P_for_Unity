const WebSocket = require('ws')

function noop() {}

function heartbeat() {
    this.isAlive = true;
    console.log('heart beat bump');
}

const wss = new WebSocket.Server({ port: 9000 },()=>{
    console.log('server started')
});

let clients = [];

wss.on('connection', function connection(wsConnection, req) {

    const id = wsConnection.remoteAddress;
    clients.push({wsConnection, id});

    wsConnection.isAlive = true;

    wsConnection.on('pong', heartbeat);

    wsConnection.on('message', function incoming(data) {

        console.log(data);
        wss.clients.forEach(function each(client) {
            if (client.readyState === WebSocket.OPEN) {
                client.send(`ip : ${req.socket.remoteAddress} port : ${req.socket.remotePort}`);
            }
        });
    });
});

wss.on('open', function open() {
    console.log('connected');
    ws.send(Date.now());
});
  
wss.on('close', function close() {
    console.log('disconnected');
    clearInterval(interval);
});

const interval = setInterval(function ping() {
    wss.clients.forEach(function each(ws) {
        if (ws.isAlive === false) return ws.terminate();

        ws.isAlive = false;
        ws.ping(noop);
    });
}, 30000);

wss.on('listening',()=>{
    console.log('listening on 9000')
})