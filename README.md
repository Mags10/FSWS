# 🚀 Ensamblador SIC/XE

[![Estado](https://img.shields.io/badge/Estado-Desarrollo-blue)](https://github.com/Mags10/FSWS)
[![Licencia](https://img.shields.io/badge/Licencia-MIT-green)](https://opensource.org/licenses/MIT)
[![Version](https://img.shields.io/badge/Versión-1.0-orange)](https://github.com/Mags10/FSWS)

Un ensamblador completo para la arquitectura SIC/XE, implementado en C# con una interfaz gráfica intuitiva para Windows Forms.

## 📋 Características

- **Análisis Léxico y Sintáctico**: Detección avanzada de errores en el código fuente usando ANTLR
- **Proceso de Ensamblado en 3 Pasos**:
  - **Paso 1**: Análisis léxico-sintáctico y construcción de tablas de símbolos
  - **Paso 2**: Generación de código objeto
  - **Paso 3**: Generación de registros de programa objeto
- **Carga y Ligado**: Capacidad para cargar y ligar múltiples programas objeto
- **Soporte Multi-sección**: Manejo completo de secciones de control (CSECT)
- **Referencias Externas**: Soporte para EXTDEF y EXTREF
- **Resaltado de Sintaxis**: Coloración de códigos de operación, registros, etiquetas y operandos
- **Visualización Detallada**: Tablas de símbolos, bloques y archivos intermedios

## 🔧 Instalación

### Requisitos previos
- Windows 7 o superior
- .NET Framework 4.5 o superior
- Visual Studio 2017 o superior (para desarrollo)

### Instrucciones
1. Clona este repositorio: `git clone https://github.com/Mags10/FSWS.git`
2. Abre la solución en Visual Studio
3. Compila el proyecto
4. Ejecuta la aplicación desde el archivo `.exe` generado

## 🚀 Uso

1. Escribe o carga un programa en lenguaje ensamblador SIC/XE en el editor
2. Ejecuta el análisis léxico-sintáctico para verificar errores
3. Ejecuta el primer paso para generar las tablas de símbolos
4. Ejecuta el segundo paso para generar código objeto
5. Visualiza las tablas de símbolos, bloques y código objeto generado
6. Utiliza la función de carga y ligado para ejecutar programas

### Ejemplo de código SIC/XE

```assembly
SUM     START   4000H
FIRST   LDX     ZERO
        LDA     ZERO
LOOP    ADD     TABLE,X
        TIX     COUNT
        JLT     LOOP
        STA     TOTAL
        RSUB
TABLE   RESW    2000
COUNT   RESW    1
ZERO    WORD    0
TOTAL   RESW    1
        END     FIRST
```

## 📊 Tablas y Estructuras

El ensamblador genera y maneja las siguientes estructuras:

- **Tabla de Símbolos**: Almacena las etiquetas y sus valores
- **Tabla de Bloques**: Mantiene información sobre los bloques de programa
- **Archivo Intermedio**: Muestra el resultado del análisis línea por línea
- **Registros de Programa Objeto**: Formato estándar SIC/XE (H, D, R, T, M, E)
- **Mapa de Memoria**: Visualización del programa cargado en memoria

## 🔄 Proceso de Ensamblado

El proceso de ensamblado sigue el algoritmo clásico de tres pasos:

1. **Análisis y construcción de tablas**:
   - Análisis léxico-sintáctico del código mediante gramáticas ANTLR
   - Creación de la tabla de símbolos
   - Asignación de direcciones a los bloques

2. **Generación de código objeto**:
   - Resolución de expresiones
   - Cálculo de desplazamientos relativos
   - Generación de código máquina

3. **Generación de archivo de programa objeto**:
   - Creación de registros H, D, R, T, M y E
   - Formato estándar para cargador

## 🔗 Características de Ligado

- Ligado de múltiples programas objeto
- Resolución de referencias externas
- Reubicación de programas en memoria
- Visualización del mapa de memoria resultante

## 👨‍💻 Autor

**Miguel Alejandro Gutiérrez Silva**

Proyecto desarrollado para la materia de Fundamentos de Software de Sistemas en la Universidad Autónoma de San Luis Potosí (UASLP).

## 📚 Recursos Adicionales

- [ANTLR (ANother Tool for Language Recognition)](https://www.antlr.org/)
- [Documentación de SIC/XE](https://en.wikipedia.org/wiki/SIC/XE)
- [System Software: An Introduction to Systems Programming - Leland L. Beck](https://www.amazon.com/System-Software-Introduction-Programming-Leland/dp/0201422549)

## 📄 Licencia

Este proyecto está licenciado bajo la Licencia MIT - ver el archivo [LICENSE](LICENSE) para más detalles.

⭐️ Si te gusta este proyecto, ¡no olvides darle una estrella en GitHub! ⭐️