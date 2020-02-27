# Tracktoria
Real-time tracker GUI for the [Netduino](https://en.wikipedia.org/wiki/MOS_Technology_6581) controlling a [MOS 6581](https://en.wikipedia.org/wiki/MOS_Technology_6581) sound chip. Final project for ELEX 267 at [Camosun College](http://camosun.ca/learn/calendar/current/web/ecet.html).

![app screenshot](https://github.com/aspck/Tracktoria/blob/master/img/window.png "app screenshot")

Features implemented:
* Real-time playback with simulated keyboard
* ADSR Envelope editor
* Support for arbitrary number of tracks

Ultimately the performance was too poor for a real-time application. It would be better to implement a simple MIDI -> 6581 adapter with a PIC microcontroller and use an established MIDI tracker software, but it was a fun experiment.
