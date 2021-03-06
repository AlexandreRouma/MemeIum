﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MemeIum.Misc;
using MemeIum.Requests;

namespace MemeIum.Services
{
    class MappingService : IMappingService
    {
        private readonly IP2PServer _server;
        private readonly ILogger Logger;
    
        public List<Peer> Peers;
        public List<RequestForPeers> ActiveRequestForPeers;

        public Peer ThisPeer;

        public MappingService()
        {
            string externalip = new WebClient().DownloadString("http://icanhazip.com");

            Peers = new List<Peer>();
            ThisPeer = new Peer(){Address = externalip,Port=Configurations.Config.MainPort};
            ActiveRequestForPeers = new List<RequestForPeers>();

            _server = Services.GetService<IP2PServer>();
            Logger = Services.GetService<ILogger>();

        }

        public void InitiateSweap(List<Peer> originPeers)
        {
            var request = new GetAddressesRequest();
            request.MaxPeers = Configurations.Config.MaxPeersGiven;

            foreach (var originPeer in originPeers)
            {
                _server.SendResponse(request,originPeer);

                ActiveRequestForPeers.Add(new RequestForPeers(){From=originPeer,ElapseTime = DateTime.Now.AddSeconds(Configurations.Config.SecondsToWaitForAddresses)});
            }
        }

        public void AddPeerToMe(Peer toadd)
        {
            if (Peers.FindAll(rr => rr.Equals(toadd)).Count == 0 && !ThisPeer.Equals(toadd))
            {
                Peers.Add(toadd);
                Logger.Log($"New peer: {toadd.ToString()}");
            }
        }

        public bool WantedAdresses(AddressesRequest req, Peer source)
        {
            var newActive = ActiveRequestForPeers.FindAll(r =>
                r.ElapseTime >= DateTime.Now);
            ActiveRequestForPeers = newActive.ToList();

            bool wanted = (ActiveRequestForPeers.FindAll(r =>
                               r.From.Equals(source)).Count > 0);
            if (!wanted)
            {
                Logger.Log($"Rejected Addresses From: {source.ToString()} | Many: {req.Peers.Count} | all: {ActiveRequestForPeers.Count}");
            }
            return wanted;
        }

        public void ParseAddressesRequest(AddressesRequest request,Peer source)
        {
            AddPeerToMe(source);
            if (WantedAdresses(request,source))
            {
                foreach (var peer in request.Peers)
                {
                    AddPeerToMe(peer);
                }
            }
        }

        public void ParseGetAddressesRequest(GetAddressesRequest request, Peer source)
        {
            //Answer with max n peers
            var peersRandom = new List<Peer>();
            peersRandom.AddRange(Peers);
            peersRandom.Shuffle();
            int numberOfPeersToRespond = (peersRandom.Count >= request.MaxPeers) ? request.MaxPeers : peersRandom.Count;
            var responsePeers = peersRandom.Take(numberOfPeersToRespond).ToList();
            var response = new AddressesRequest(){Peers = responsePeers };

            AddPeerToMe(source);
            _server.SendResponse(response,source);
        }

        public void Broadcast()
        {

        }

    }
}
