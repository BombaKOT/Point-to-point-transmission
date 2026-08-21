P2P Transmission - Sockets Only

This is a simple console application for transmitting files between two willing hosts as it allows you to configure your communications manually.
This project is made to be easily modifiable, providing base class for transmissions in "DriveTransmitions," and simple modification template of
ConsoleManager => ServerManager => DriveTransmitions. 

HOW TO USE
  
  Have 2 or more willing hosts running the programs periodically:
    To send a file from one point to another, make sure the recipient is running "receive" before the sender completes the "send" query.
    To connect, make sure the host is running "connects" before the client queries "connectc"
    
  Make sure any problematic filters are off. EX: on Linux systems you might want to disable UFW and on MacOS turn off the default firewall.
    They can prevent successful communication between two hosts, or block them from connecting entirely. 

  
  
