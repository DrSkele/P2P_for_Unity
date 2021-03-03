const Port = 9000;
const Ip = '117.52.31.243';

const dgram = require('dgram');
const server = dgram.createSocket('udp4');

var clientList = [];

class Client{
    constructor(localIP, publicAddress, publicPort){
        this.localIP = localIP;
        this.publicAddress = publicAddress;
        this.publicPort = publicPort;
        this.isAlive = true;
    }
}

server.on('listening', function () {
    let address = server.address();
    console.log(`server listening on ${address.address} : ${address.port}`);
})

server.on('message', function (message, remote) {
    
    var publicAddress = remote.address;
    var publicPort = remote.port;

    var data = JSON.parse(message);

    console.log(data);

    switch(data.Header) {
        case 'request_handshake':
            //Message format : 'ipaddress:port'
            var localIP = data.Message;

            var hasDuplicate = false;
            for(var i = 0; i <clientList.length; i++) {
                if(clientList[i].IpAddress == data.IpAddress){
                    hasDuplicate = true;
                    break;
                }
            }
            if(!hasDuplicate) {
                clientList.push(new Client(localIP, publicAddress, publicPort));
            }
            server.send(JSON.stringify({
                Header : 'response_handshake',
                Message : `${publicAddress}:${publicPort}`,
                Time : Date.now()
            }), remote.port, remote.address);
            break;

        case 'request_list' :
            clientList.forEach(element => {
                server.send(element, remote.port, remote.address);
            });

        case 'ping' :
            var liveClient = clientList.find(x => x.publicAddress == publicAddress);
            liveClient.isAlive = true;
            break;
        default :
            server.send(`${publicAddress}:${publicPort}`, remote.port, remote.address);
            
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
    clientList = clientList.filter(client => {
        return client.isAlive == true;
    })

    clientList.forEach(client => {
        client.isAlive = false;
        console.debug(client.publicPort);
        server.send(JSON.stringify({
            Header : 'ping',
        }), client.publicPort, client.publicAddress);
    });
}, 30000);
