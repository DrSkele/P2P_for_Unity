const Port = 9000;
const Ip = '117.52.31.243';

const dgram = require('dgram');
const server = dgram.createSocket('udp4');

var clientList = [];

class Client{
    constructor(clientIPPair){
        this.IPPair = clientIPPair;
        this.isAlive = true;
    }
}

class IPPair{
    constructor(LocalIP, PublicIP, Port){
        this.LocalIP = LocalIP;
        this.PublicIP = PublicIP;
        this.Port = Port;
    }
}

class UdpPacket{
    constructor(Header, Payload){
        this.Header = Header;
        this.Payload = Payload;
        this.Time = Date.Time.now;
    }
}

server.on('listening', function () {
    let address = server.address();
    console.log(`server listening on ${address.address} : ${address.port}`);
})

server.on('message', function (receivedPacket, remote) {
    
    var remotePublicIP = remote.address;
    var remotePublicPort = remote.port;

    try{
        
        var jsonPacket = JSON.parse(receivedPacket);
        //UdpPacket
        //{
        //  Header : 
        //  Payload :
        //  Time   :    
        //}

        console.log(jsonPacket);

        switch(jsonPacket.Header) {
            case 'request_handshake':
                var receivedIPPair = JSON.parse(jsonPacket.Payload);
                //IPPair
                //{
                //  LocalIP : 
                //  PublicIP :
                //  Port   :    
                //}
                var remoteLocalIP = receivedIPPair.LocalIP;
                var clientIPPair = new IPPair(remoteLocalIP, remotePublicIP, remotePublicPort);

                var hasDuplicate = false;
                clientList.forEach(client => {
                    if(client.IPPair.PublicIP == remotePublicIP){
                        hasDuplicate = true;
                        break;
                    }
                });
                if(!hasDuplicate) {
                    clientList.push(new Client(clientIPPair));
                }
                server.send(
                    JSON.stringify(
                        new UdpPacket(
                            'response_handshake', 
                            JSON.stringify(clientIPPair)
                        )
                    ), remote.port, remote.address
                );
                break;
            case 'request_list' :
                server.send(
                    JSON.stringify(
                        new UdpPacket(
                            'response_list',
                            JSON.stringify(
                                clientList
                                .filter(client => { return client.publicIP != remotePublicIP })
                                .map(peer => JSON.stringify(new IPPair(peer.IPPair.LocalIP, peer.IPPair.PublicIP, peer.IPPair.Port)))
                            ),
                        )
                    ), remote.port, remote.address
                );
            case 'request_connection':
                var receivedIPPair = JSON.parse(jsonPacket.Payload);

                server.send(
                    JSON.stringify(
                        new UdpPacket(
                            'request_connection', 
                            clientList.find(client => client.IPPair.PublicIP == remotePublicIP)
                        )
                    ), receivedIPPair.Port, receivedIPPair.PublicIP
                )
            case 'response_connection':
                var receivedIPPair = JSON.parse(jsonPacket.Payload);

                server.send(
                    JSON.stringify(
                        new UdpPacket(
                            'response_connection', 
                            clientList.find(client => client.IPPair.PublicIP == remotePublicIP)
                        )
                    ), receivedIPPair.Port, receivedIPPair.PublicIP
                )
            case 'pong' :
                var liveClient = clientList.find(x => x.publicAddress == remotePublicIP);
                liveClient.isAlive = true;
                break;
            default :
                server.send(JSON.stringify(
                    new UdpPacket(
                        'none', 
                        receivedPacket
                    )
                ), remote.port, remote.address);
                
        }    
    }
    catch
    {
        server.send(`${remotePublicIP}:${remotePublicPort}`, remote.port, remote.address);
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
        server.send(
            JSON.stringify(
                new UdpPacket('ping', '')
            ), client.publicPort, client.publicAddress
        );
    });
}, 30000);
