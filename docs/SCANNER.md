# Scanner (WIA)

> **Status:** a ser preenchido na Fase 4.

Este documento explicará, quando a Fase 4 for implementada: WIA (Windows Image Acquisition),
descoberta de scanners, aquisição, resolução, origem, tratamento de erros (scanner desconectado,
ocupado, sem driver) e a abstração `IScannerService` que permite adicionar outras tecnologias de
digitalização no futuro sem alterar a UI.
