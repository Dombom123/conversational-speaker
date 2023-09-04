import time
from rpi_ws281x import PixelStrip, Color
import argparse
import threading

# LED strip configuration:
LED_COUNT = 20
LED_PIN = 12
LED_FREQ_HZ = 800000
LED_DMA = 10
LED_BRIGHTNESS = 100
LED_INVERT = False
LED_CHANNEL = 0

stop_event = threading.Event()


def colorWipe(strip, color, wait_ms=50):
    for i in range(strip.numPixels()):
        strip.setPixelColor(i, color)
        strip.show()
        time.sleep(wait_ms / 1000.0)


def wheel(pos):
    if pos < 85:
        return Color(pos * 3, 255 - pos * 3, 0)
    elif pos < 170:
        pos -= 85
        return Color(255 - pos * 3, 0, pos * 3)
    else:
        pos -= 170
        return Color(0, pos * 3, 255 - pos * 3)


def set_color(strip, color):
    for i in range(strip.numPixels()):
        strip.setPixelColor(i, color)
    strip.show()


def idle_state(strip):
    while not stop_event.is_set():
        colorWipe(strip, Color(255, 255, 255), 500)


def listening_state(strip):
    while not stop_event.is_set():
        set_color(strip, Color(0, 255, 0))
        time.sleep(0.5)


def thinking_state(strip):
    while not stop_event.is_set():
        set_color(strip, Color(0, 0, 255))
        time.sleep(0.5)


def responding_state(strip):
    while not stop_event.is_set():
        set_color(strip, Color(255, 0, 0))
        time.sleep(0.5)


def clear_state(strip):
    set_color(strip, Color(0, 0, 0))


def start_animation(action_func, strip):
    global stop_event
    stop_event.set()
    time.sleep(0.5)
    stop_event.clear()
    threading.Thread(target=action_func, args=(strip,)).start()


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('-c', '--clear', action='store_true', help='clear the display on exit')
    parser.add_argument('-a', '--action', type=str, help='LED action to perform', required=True)
    args = parser.parse_args()

    strip = PixelStrip(LED_COUNT, LED_PIN, LED_FREQ_HZ, LED_DMA, LED_INVERT, LED_BRIGHTNESS, LED_CHANNEL)
    strip.begin()

    if args.action == 'idle':
        start_animation(idle_state, strip)
    elif args.action == 'listening':
        start_animation(listening_state, strip)
    elif args.action == 'thinking':
        start_animation(thinking_state, strip)
    elif args.action == 'responding':
        start_animation(responding_state, strip)
    elif args.action == 'clear':
        clear_state(strip)

    if args.clear:
        colorWipe(strip, Color(0, 0, 0), 10)
