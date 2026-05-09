OpenBanapass Card Switcher - supports Android NFC, card reader, and manual switching


1: Run cmd as an Administrator and run the following commands:

netsh http add urlacl url=http://+:7979/nfc/ user=Everyone

netsh advfirewall firewall add rule name="WMMT6 CardManager Webhook" dir=in action=allow protocol=TCP localport=7979


2.Copy the contents of this folder to the same directory where your card.ini resides
3.Run CardManagerOpenBana.exe

----ANDROID PHONE TUTORIAL----
1. Download the app called Tasker on the PlayStore
1a. Watch YouTube tutorials on the basics on how this app works.
2. Create the following Profile:
	Event > NFC Tag
	Add Action > HTTP Request
	Set the following options
METHOD: POST
URL: http://<local ip address of where you have CardManagerOpenBana Running>:7979/nfc
	for example: "http://192.168.0.123:7979/nfc"
Headers: Content-Type:application/json
Body: {"uid":"{evtprm1}"}

3. Save it and enable tasker profile. When you scan an NFC card, it will send it's tagid to the app running on your PC. 

Note: This only replaces the card.ini file, you still have to press the C button on your keyboard when it asks to scan banapass. Future enhancement maybe to press C for you when a card is scanned.

This has been working for me. I'm able to swap cards.

Build your own if you'd like. Open source.

