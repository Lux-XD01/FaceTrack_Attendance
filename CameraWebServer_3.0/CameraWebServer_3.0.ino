#include <Arduino.h>
#include "esp_camera.h"
#include <WiFi.h>
#include <esp_wifi.h>
#include <WiFiManager.h>   // tzapu
#include <ESPmDNS.h>
#include <WiFiUdp.h>
#include "board_config.h"

// ===================== VENTILADOR =====================
const int      PIN_FAN      = D0;      // GPIO1 de la XIAO ESP32-S3
const uint32_t FAN_PWM_HZ   = 25000;   // 25 kHz: inaudible
const uint8_t  FAN_PWM_BITS = 8;       // duty 0..255

const float T_ENCENDER = 62.0;   // el ventilador arranca al llegar a esta temperatura
const float T_APAGAR   = 55.0;   // y se apaga recién al bajar de esta (histéresis de 7 °C)
const float T_MAXIMO   = 72.0;   // desde acá gira al 100 %
const uint8_t DUTY_MINIMO = 100; // ~40 %: por debajo, muchos ventiladores de 2 hilos se frenan
const unsigned long FAN_PERIODO_MS = 2000;   // se evalúa cada 2 s

bool ventiladorEncendido = false;
float tempFiltrada = NAN;
unsigned long tUltimaEvaluacion = 0;

void actualizarVentilador() {
  unsigned long ahora = millis();
  if (ahora - tUltimaEvaluacion < FAN_PERIODO_MS) return;
  tUltimaEvaluacion = ahora;

  float t = temperatureRead();                      // temperatura del chip (°C)
  if (isnan(t) || t < -20 || t > 120) {             // lectura inválida: por seguridad, encendido
    digitalWrite(PIN_FAN, HIGH);
    ventiladorEncendido = true;
    return;
  }
  // Suaviza el ruido de la lectura
  tempFiltrada = isnan(tempFiltrada) ? t : (0.7f * tempFiltrada + 0.3f * t);

  // Histéresis: umbrales DISTINTOS para encender y apagar, así no oscila
  if (!ventiladorEncendido && tempFiltrada >= T_ENCENDER)      ventiladorEncendido = true;
  else if (ventiladorEncendido && tempFiltrada <= T_APAGAR)    ventiladorEncendido = false;

  // Solo ON/OFF: salida digital pura (3,3 V fijos o 0 V), sin valores intermedios
  digitalWrite(PIN_FAN, ventiladorEncendido ? HIGH : LOW);
  Serial.printf("[FAN] T=%.1f C  %s\n", tempFiltrada, ventiladorEncendido ? "ON" : "OFF");
}

// ===================== ORIENTACIÓN DE LA CÁMARA =====================
// OV3660: de fábrica la imagen sale invertida verticalmente (por eso vflip=1 = "derecha").
//   VFLIP=1, HMIRROR=0 -> normal
//   VFLIP=0, HMIRROR=1 -> rotada 180° (cámara montada al revés)
//   VFLIP=1, HMIRROR=1 -> normal pero en espejo
//   VFLIP=0, HMIRROR=0 -> boca abajo
#define CAM_VFLIP    0
#define CAM_HMIRROR  1

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
    pinMode(PIN_FAN, OUTPUT);                         // ventilador ON/OFF (sin PWM)
    digitalWrite(PIN_FAN, LOW);                       // arranca apagado

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
    WiFi.setTxPower(WIFI_POWER_19_5dBm);                 // máximo disponible (19,5 dBm)
  esp_wifi_set_bandwidth(WIFI_IF_STA, WIFI_BW_HT20);   // canal de 20 MHz: más estable en 2,4 GHz
  int8_t potencia = 0;
  esp_wifi_get_max_tx_power(&potencia);
  Serial.printf("TX power: %.2f dBm\n", potencia * 0.25f);
  Serial.println("WiFi conectado a: " + WiFi.SSID());

  if (MDNS.begin(NOMBRE_HOST)) MDNS.addService("http", "tcp", 80);
  udp.begin(PUERTO_BEACON);

 camera_config_t config;
  config.ledc_channel = LEDC_CHANNEL_7;
  config.ledc_timer = LEDC_TIMER_3;
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
      config.jpeg_quality = 10; //<--CALIDAD DEL JPEG (menor número = mejor calidad, más pesado)
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
    s->set_vflip(s, CAM_VFLIP);
    s->set_hmirror(s, CAM_HMIRROR); 
    s->set_brightness(s, 1);   // up the brightness just a bit
    s->set_saturation(s, -2);  // lower the saturation
  }
  // drop down frame size for higher initial frame rate
  if (config.pixel_format == PIXFORMAT_JPEG) {
    //------------------------------------------------------------------
    // Esta es la resolución REAL con la que arranca el stream (480x320).

    s->set_framesize(s, FRAMESIZE_VGA);  // 640x480 (antes HVGA 480x320)
    s->set_vflip(s, CAM_VFLIP);          // reaplicar tras el cambio de resolución
    s->set_hmirror(s, CAM_HMIRROR);    //------------------------------------------------------------------
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
    actualizarVentilador();
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
