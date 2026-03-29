When some users launch Marine, they will be greeted by the following error message:
![](/img/instructions/Screenshot_2024-11-20_224213.png)

This problem occurs when your Internet Service Provider, DNS, VPN, or your Router blocks the Marine domain, or vice versa (maybe your VPN's IP might often be flagged by Cloudflare).

### Solutions

1. Change the DNS your operating system uses
   * We recommend Cloudflare's 1.1.1.1 DNS. You can find instructions on how to [change your DNS to Cloudflare's DNS here.](https://one.one.one.one/dns/)
2. Turn off your VPN
   * Marine does not store your IP in our database.(besides the ways outlined in our [privacy policy.](/auth/privacy))
3. Turn on a VPN
   * Sometimes, your IP can be flagged as malicious by Cloudflare. Turning on a VPN can resolve this.