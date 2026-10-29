#include <Arduino.h>
#include "esp_camera.h"
#include <WiFi.h>
#include <WiFiManager.h>   // tzapu
#include <ESPmDNS.h>
#include <WiFiUdp.h>
#include "board_config.h"

// ---------- Configuración de red ----------
#define NOMBRE_HOST        "facetrack"          // http://facetrack.local
#define AP_NOMBRE          "FaceTrack-Setup"    // red temporal de configuración
#define AP_CLAVE           "facetrack123"       // mínimo 8 caracteres
#define PIN_BOTON_WIFI     0                    // botón BOOT de la XIAO (GPIO0)
#define MS_MANTENER_RESET  5000                 // mantener 5 s para borrar el WiFi
#define PUERTO_BEACON      4210                 // debe coincidir con la app C#
#define FIRMA_BEACON       "FACETRACK|XIAO01"   // debe coincidir con la app C#

WiFiUDP udp;

void startCameraServer();
void setupLedFlash();

void setup() {
  setCpuFrequencyMhz(160);   // 240 -> 160 MHz: menos calor, el Wi-Fi funciona igual
  Serial.begin(115200);
  Serial.setDebugOutput(true);
  Serial.println();

  pinMode(PIN_BOTON_WIFI, INPUT_PULLUP);
  pinMode(LED_BUILTIN, OUTPUT);
  digitalWrite(LED_BUILTIN, HIGH);          // en la XIAO, HIGH = apagado

  WiFi.setHostname(NOMBRE_HOST);
  WiFi.mode(WIFI_STA);
  
  WiFiManager wm;
  wm.setConnectTimeout(20);                 // 20 s para conectar a la red guardada
  wm.setConfigPortalTimeout(180);           // portal abierto 3 min; si nadie configura, reinicia
  wm.setAPCallback([](WiFiManager *w) {
    Serial.println("MODO CONFIGURACION: conectate a '" AP_NOMBRE "' y abri http://192.168.4.1");
    digitalWrite(LED_BUILTIN, LOW);         // LED encendido fijo = portal abierto
  });

  Serial.println("Conectando WiFi...");
  bool ok = wm.autoConnect(AP_NOMBRE, AP_CLAVE);
  digitalWrite(LED_BUILTIN, HIGH);
  if (!ok) {
    Serial.println("Sin configuracion WiFi. Reiniciando...");
    delay(1000);
    ESP.restart();
  }

  WiFi.setSleep(false);
  WiFi.setTxPower(WIFI_POWER_15dBm);   // menos potencia = menos calor
  Serial.println("WiFi conectado a: " + WiFi.SSID());

  if (MDNS.begin(NOMBRE_HOST)) MDNS.addService("http", "tcp", 80);
  udp.begin(PUERTO_BEACON);

 camera_config_t config;
  config.ledc_channel = LEDC_CHANNEL_0;
  config.ledc_timer = LEDC_TIMER_0;
  config.pin_d0 = Y2_GPIO_NUM;
  config.pin_d1 = Y3_GPIO_NUM;
  config.pin_d2 = Y4_GPIO_NUM;
  config.pin_d3 = Y5_GPIO_NUM;
  config.pin_d4 = Y6_GPIO_NUM;
  config.pin_d5 = Y7_GPIO_NUM;
  config.pin_d6 = Y8_GPIO_NUM;
  config.pin_d7 = Y9_GPIO_NUM;
  config.pin_xclk = XCLK_GPIO_NUM;
  config.pin_pclk = PCLK_GPIO_NUM;
  config.pin_vsync = VSYNC_GPIO_NUM;
  config.pin_href = HREF_GPIO_NUM;
  config.pin_sccb_sda = SIOD_GPIO_NUM;
  config.pin_sccb_scl = SIOC_GPIO_NUM;
  config.pin_pwdn = PWDN_GPIO_NUM;
  config.pin_reset = RESET_GPIO_NUM;
  config.xclk_freq_hz = 20000000;
  config.frame_size = FRAMESIZE_UXGA;
  config.pixel_format = PIXFORMAT_JPEG;  // for streaming
  //config.pixel_format = PIXFORMAT_RGB565; // for face detection/recognition
  config.grab_mode = CAMERA_GRAB_WHEN_EMPTY;
  config.fb_location = CAMERA_FB_IN_PSRAM;
  config.jpeg_quality = 12;
  config.fb_count = 1;

  // if PSRAM IC present, init with UXGA resolution and higher JPEG quality
  //                      for larger pre-allocated frame buffer.
  if (config.pixel_format == PIXFORMAT_JPEG) {
    if (psramFound()) {
      config.jpeg_quality = 12; //<--CALIDAD DEL JPEG
      config.fb_count = 2; 
      config.grab_mode = CAMERA_GRAB_LATEST;
    } else {
      // Limit the frame size when PSRAM is not available
      config.frame_size = FRAMESIZE_SVGA;  
      config.fb_location = CAMERA_FB_IN_DRAM;
    }
  } else {
    // Best option for face detection/recognition
    config.frame_size = FRAMESIZE_240X240;
#if CONFIG_IDF_TARGET_ESP32S3
    config.fb_count = 2;
#endif
  }

#if defined(CAMERA_MODEL_ESP_EYE)
  pinMode(13, INPUT_PULLUP);
  pinMode(14, INPUT_PULLUP);
#endif

    // camera init
  esp_err_t err = esp_camera_init(&config);
  if (err != ESP_OK) {
    Serial.printf("Camera init failed with error 0x%x. Reiniciando...\n", err);
    delay(2000);
    ESP.restart();
  }

  sensor_t *s = esp_camera_sensor_get();
  // initial sensors are flipped vertically and colors are a bit saturated
  if (s->id.PID == OV3660_PID) {
    s->set_vflip(s, 1);        // flip it back
    s->set_brightness(s, 1);   // up the brightness just a bit
    s->set_saturation(s, -2);  // lower the saturation
  }
  // drop down frame size for higher initial frame rate
  if (config.pixel_format == PIXFORMAT_JPEG) {
    //------------------------------------------------------------------
    // Esta es la resolución REAL con la que arranca el stream (480x320).

    s->set_framesize(s, FRAMESIZE_HVGA); //antes era QVGA
    //------------------------------------------------------------------
  }

#if defined(CAMERA_MODEL_M5STACK_WIDE) || defined(CAMERA_MODEL_M5STACK_ESP32CAM)
  s->set_vflip(s, 1);
  s->set_hmirror(s, 1);
#endif

#if defined(CAMERA_MODEL_ESP32S3_EYE)
  s->set_vflip(s, 1);
#endif

// Setup LED FLash if LED pin is defined in camera_pins.h
#if defined(LED_GPIO_NUM)
  setupLedFlash();
#endif

  startCameraServer();

  Serial.print("Camera Ready! Use 'http://");
  Serial.print(WiFi.localIP());
  Serial.println("' to connect");
  
}

void loop() {
  static uint32_t tBeacon = 0, tBoton = 0, tSinWifi = 0;
  uint32_t ahora = millis();

  // 1) Botón: mantener 5 s => borrar credenciales y abrir el portal
  if (digitalRead(PIN_BOTON_WIFI) == LOW) {
    if (tBoton == 0) tBoton = ahora;
    if (ahora - tBoton >= MS_MANTENER_RESET) {
      Serial.println("Borrando credenciales WiFi...");
      for (int i = 0; i < 6; i++) { digitalWrite(LED_BUILTIN, i % 2); delay(100); }
      WiFiManager wm;
      wm.resetSettings();
      while (digitalRead(PIN_BOTON_WIFI) == LOW) delay(10);  // esperar a que sueltes
      ESP.restart();
    }
  } else {
    tBoton = 0;
  }

  // 2) Beacon UDP: avisa su IP a la app cada 2 s
  if (WiFi.status() == WL_CONNECTED) {
    tSinWifi = 0;
    if (ahora - tBeacon >= 2000) {
      tBeacon = ahora;
      IPAddress bcast((uint32_t)WiFi.localIP() | ~(uint32_t)WiFi.subnetMask());
      udp.beginPacket(bcast, PUERTO_BEACON);
      udp.print(String(FIRMA_BEACON) + "|" + String(WiFi.RSSI()) + "|" + WiFi.SSID());
      udp.endPacket();
    }
  } else {
    // 3) Si pierde el WiFi más de 2 min, reinicia (vuelve a intentar o abre el portal)
    if (tSinWifi == 0) tSinWifi = ahora;
    if (ahora - tSinWifi > 120000) ESP.restart();
  }

  delay(50);
}
