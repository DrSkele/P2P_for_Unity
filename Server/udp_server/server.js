const Port = 9000;
const Ip = '117.52.31.243';

const dgram = require('dgram');
const server = dgram.createSocket('udp4');

var clientList = [];

server.on('listening', function () {
    let address = server.address();
    console.log(`server listening on ${address.address} : ${address.port}`);
})

server.on('message', function (message, remote) {
    
    var ipAddress = `${remote.address}:${remote.port}`;

    var data = JSON.parse(message);

    console.log(data);

    switch(data.Header) {
        case 'HandShake':

            var hasDuplicate = false;
            for(var i = 0; i <clientList.length; i++) {
                if(clientList[i].IpAddress == data.IpAddress){
                    hasDuplicate = true;
                    break;
                }
            }
            if(!hasDuplicate) {
                clientList.push({
                    LocalAddress : data.IpAddress,
                    PublicAddress : remote.address,
                    PublicPort : remote.port
                });
            }
            server.send(JSON.stringify({
                Header : 'HandShake',
                IpAddress : ipAddress
            }), remote.port, remote.address);
            break;

        case 'Request' :
            clientList.forEach(element => {
                server.send(element, remote.port, remote.address);
            });
        default :
            server.send(ipAddress, remote.port, remote.address);
            
    }    
})

server.on('error', function (error) {
    console.log(`Error : ${error}`);
    server.close();
});

server.on('close', function() {
    clearInterval(interval);
})

server.bind(Port);

const interval = setInterval(function ping() {
    clientList.forEach(function each(client) {
        server.send(JSON.stringify({
            Header : 'Ping',
        }), client.PublicPort, client.PublicAddress);
    });
}, 30000);
