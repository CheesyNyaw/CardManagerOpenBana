**OpenBanapass Card Switcher - supports Android NFC, card reader, and manual switching**
Quick code written by my buddy Claude. No support for now, use at your own risk.

I've created a small program with the help of my buddy Claude that handles card.ini switching allowing you to switch between multiple emulated cards. 

This program is also NFC capable, allowing you to switch between cards using NFC tags using your phone. You can tap an NFC card on your phone and it will send the tagid to the app, switching the card.ini based on this. This acts like an emulated banapass terminal.

So far, I'm using an Android phone to send a POST request to the app, using the NFC's tagid, to switch the registered .ini.

The phone uses an app called Tasker on the Play Store.

I'm also planning on implementing support for a USB NFC card reader.

**Steps:**

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
URL: http://<localipaddressofwhereyouhaveCardManagerOpenBanaRunning>:7979/nfc
	for example: "http://192.168.0.123:7979/nfc"
Headers: Content-Type:application/json
Body: {"uid":"{evtprm1}"}

3. Save it and enable tasker profile. When you scan an NFC card, it will send it's tagid to the app running on your PC. 

_Note: This only replaces the card.ini file, you still have to press the C button on your keyboard when it asks to scan banapass. Future enhancement maybe to press C for you when a card is scanned._

This has been working for me. I'm able to swap cards.

Build your own if you'd like. Open source.

