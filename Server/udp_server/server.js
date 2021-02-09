const Port = 9000;
const Ip = '127.0.0.1';

const dgram = require('dgram');
const server = dgram.createSocket('udp4');

server.on('listening', function () {
    let address = server.address();
    console.log(`server listening on ${address.address} : ${address.port}`);
})

server.on('message', function (message, remote) {
    console.log(`message "${message}" from ${remote.address} : ${remote.port}`);
    server.send(message, remote.port, remote.address);
    server.send('hello from node js!', remote.port, remote.address);
})

server.on('error', function (error) {
    console.log(`Error : ${error}`);
    server.close();
});

server.bind(Port, Ip);