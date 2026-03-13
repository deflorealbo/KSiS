#include <iostream>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <chrono>

#pragma comment(lib, "ws2_32.lib")

using namespace std;

#define ICMP_ECHO 8
#define ICMP_ECHOREPLY 0
#define ICMP_TIME_EXCEEDED 11

#define MAX_HOPS 30
#define PACKET_SIZE 32
#define TIMEOUT 3000

struct ICMPHeader
{
    BYTE type;
    BYTE code;
    USHORT checksum;
    USHORT id;
    USHORT seq;
};

unsigned short CalculateChecksum(unsigned short* buffer, int size)
{
    unsigned long sum = 0;

    while (size > 1)
    {
        sum += *buffer++;
        size -= 2;
    }

    if (size)
        sum += *(BYTE*)buffer;

    sum = (sum >> 16) + (sum & 0xFFFF);
    sum += (sum >> 16);

    return (unsigned short)(~sum);
}

bool ResolveHostname(const string& host, sockaddr_in& addr)
{
    addr.sin_family = AF_INET;

    if (inet_pton(AF_INET, host.c_str(), &addr.sin_addr) == 1)
        return true;

    addrinfo hints{}, * result;

    hints.ai_family = AF_INET;

    if (getaddrinfo(host.c_str(), nullptr, &hints, &result) != 0)
        return false;

    addr.sin_addr = ((sockaddr_in*)result->ai_addr)->sin_addr;

    freeaddrinfo(result);

    return true;
}

string ReverseDNS(sockaddr_in& addr)
{
    char host[NI_MAXHOST];

    if (getnameinfo((sockaddr*)&addr, sizeof(addr),
        host, sizeof(host),
        nullptr, 0, NI_NAMEREQD) == 0)
    {
        return host;
    }

    return "";
}

void TraceRT(sockaddr_in destination, bool resolveNames)
{
    SOCKET sock = socket(AF_INET, SOCK_RAW, IPPROTO_ICMP);

    if (sock == INVALID_SOCKET)
    {
        cout << "Raw socket creation failed\n";
        return;
    }

    int timeout = TIMEOUT;
    setsockopt(sock, SOL_SOCKET, SO_RCVTIMEO, (char*)&timeout, sizeof(timeout));

    char sendBuffer[sizeof(ICMPHeader) + PACKET_SIZE];
    char recvBuffer[1024];

    ICMPHeader* icmp = (ICMPHeader*)sendBuffer;

    icmp->type = ICMP_ECHO;
    icmp->code = 0;
    icmp->id = (USHORT)GetCurrentProcessId();

    memset(sendBuffer + sizeof(ICMPHeader), 'A', PACKET_SIZE);

    for (int ttl = 1; ttl <= MAX_HOPS; ttl++)
    {
        setsockopt(sock, IPPROTO_IP, IP_TTL, (char*)&ttl, sizeof(ttl));

        cout << ttl;

        bool reached = false;
        sockaddr_in replyAddr{};

        for (int probe = 0; probe < 3; probe++)
        {
            icmp->seq = htons((ttl - 1) * 3 + probe);
            icmp->checksum = 0;
            icmp->checksum = CalculateChecksum((unsigned short*)sendBuffer, sizeof(sendBuffer));

            auto start = chrono::high_resolution_clock::now();

            sendto(sock, sendBuffer, sizeof(sendBuffer), 0,
                (sockaddr*)&destination, sizeof(destination));

            sockaddr_in tempAddr{};
            int addrLen = sizeof(tempAddr);

            int result = recvfrom(sock, recvBuffer, sizeof(recvBuffer), 0,
                (sockaddr*)&tempAddr, &addrLen);

            if (result == SOCKET_ERROR)
            {
                cout << "   *";
            }
            else
            {
                auto end = chrono::high_resolution_clock::now();
                double ms = chrono::duration<double, milli>(end - start).count();

                int ipHeaderLen = (recvBuffer[0] & 0x0F) * 4;

                ICMPHeader* icmpReply = (ICMPHeader*)(recvBuffer + ipHeaderLen);

                if (icmpReply->type == ICMP_TIME_EXCEEDED || icmpReply->type == ICMP_ECHOREPLY)
                {
                    cout << "   " << (int)ms << " ms";

                    replyAddr = tempAddr;

                    if (icmpReply->type == ICMP_ECHOREPLY)
                        reached = true;
                }
                else
                {
                    cout << "   *";
                }
            }
        }

        if (replyAddr.sin_addr.s_addr != 0)
        {
            char ip[INET_ADDRSTRLEN];
            inet_ntop(AF_INET, &replyAddr.sin_addr, ip, sizeof(ip));

            if (resolveNames)
            {
                string name = ReverseDNS(replyAddr);
                if (!name.empty())
                    cout << "   " << name;
            }

            cout << " [" << ip << "]";
        }

        cout << endl;

        if (reached)
        {
            cout << "\nTrace complete\n";
            closesocket(sock);
            return;
        }
    }

    closesocket(sock);
}
int main(int argc, char* argv[])
{
    if (argc < 2)
    {
        cout << "Usage:\n";
        cout << "mytracert <host> [-d]\n";
        cout << "-d  disable reverse DNS\n";
        return 0;
    }

    string host = argv[1];
    bool resolveNames = true;

    if (argc > 2 && string(argv[2]) == "-d")
        resolveNames = false;

    WSADATA wsa;
    WSAStartup(MAKEWORD(2, 2), &wsa);

    sockaddr_in destination{};

    if (!ResolveHostname(host, destination))
    {
        cout << "Cannot resolve host\n";
        return 1;
    }

    char ip[INET_ADDRSTRLEN];
    inet_ntop(AF_INET, &destination.sin_addr, ip, sizeof(ip));

    cout << "Tracing route to " << host << " [" << ip << "]\n\n";

    TraceRT(destination, resolveNames);

    WSACleanup();
}