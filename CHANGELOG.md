# Changelog

## Unreleased

Network protocol 7: all players need the same version of the mod.

- The fight time no longer counts pauses.
- Results and kill feed messages are shown in each player's own language.
- In "Free for all", fighters of different players are no longer merged into one side in the results.
- The host no longer declares a false draw while the arena is outside the area the game updates around them;
  the result waits until the host returns.
- When a guest starts the fight right after releasing fighters, the host waits a few seconds for them to appear;
  if the fight still does not start, the guest is told why.
- Statistics are sent to guests as changed rows only.
- Fighters of an arena run by another version of the mod are no longer removed as orphans after 30 seconds.
- Clearing the arena no longer lowers the world's active boss count twice for the same boss.
- Changing a live fighter's profile starts from the vanilla creature, so nothing from the previous profile remains.
- Weak spots with the same name are edited separately; attacks are matched by item prefab only.
- Creatures spawned by a destroyed object are no longer recruited to the side of the fighter who broke it.
- Only the arena host, or guests when allowed, can change a fighter's profile.
- Guests see the host's countdown in the rules.
- Number fields ignore NaN and infinity; AltGr characters can be typed in text fields.
- Profile and scenario lists are no longer read from disk several times per frame; profile names are matched
  regardless of letter case.
- A fighter left at zero health without dying is killed, and repeated recruiting of the same intruder works again.
- PatchCheck also verifies AccessTools.Field and the parameter types given to AccessTools.Method.

Сетевой протокол 7: у всех игроков должна быть одна версия мода.

- Время боя не учитывает паузы.
- Итоги и лента убийств показываются на языке каждого игрока.
- В режиме «Все против всех» бойцы разных игроков больше не сливаются в итогах в одну сторону.
- Хозяин больше не объявляет ложную ничью, пока арена вне зоны, которую игра обновляет вокруг него: итог
  дождётся его возвращения.
- Если гость начинает бой сразу после выпуска бойцов, хозяин несколько секунд ждёт их появления; если бой так
  и не начался, гость узнаёт почему.
- Статистика уходит гостям только изменившимися строками.
- Бойцы арены, которую ведёт другая версия мода, больше не удаляются через 30 секунд как бойцы исчезнувшей арены.
- Очистка арены больше не снимает одного босса со счётчика активных боссов мира дважды.
- Смена профиля живому бойцу начинается от ванильного существа: от прежнего профиля ничего не остаётся.
- Одноимённые слабые места настраиваются по отдельности; атаки сопоставляются только по префабу предмета.
- Существа из разрушенного объекта больше не встают на сторону бойца, который его разрушил.
- Профиль бойца меняет только хозяин арены или, если он разрешил, гость.
- Гость видит в правилах отсчёт хозяина.
- Числовые поля не принимают NaN и бесконечность; в текстовых полях набираются символы через AltGr.
- Списки профилей и сценариев больше не читаются с диска по нескольку раз за кадр; имена профилей не зависят
  от регистра.
- Боец с нулевым здоровьем, который не погиб, погибает; повторное вовлечение того же чужака снова работает.
- PatchCheck проверяет и AccessTools.Field, и типы параметров в AccessTools.Method.

## 1.4.0

- F1 hides and shows the game's own interface: HUD, minimap, bars over creatures, game messages, chat and key
  hints. The arena overlay, the arena's messages and the mod's windows stay on screen. The same option is in the
  Camera tab, and the key can be changed in the config.
- With Ctrl held, the mod's function keys no longer react, so the game's own Ctrl combinations work undisturbed.

- F1 скрывает и показывает интерфейс самой игры: HUD, миникарту, полоски над существами, сообщения игры, чат
  и подсказки клавиш. Оверлей арены, её сообщения и окна мода остаются на экране. Тот же флажок есть на вкладке
  «Камера», а клавиша меняется в настройках.
- При зажатом Ctrl функциональные клавиши мода больше не срабатывают, и сочетания самой игры с Ctrl работают
  без помех.

## 1.3.1

Fixes:

- The roster, scenarios and profile edits to attacks, resistances and weak spots are saved again, and guests
  now receive the host's arena rules.
- A profile saved by 1.3.0 no longer resets the creature's resistances to "normal".
- A click no longer lands on a neighboring button when a list changes at that moment, for example when
  a fighter dies or a group is added.
- Clicking buttons that change the window no longer causes errors in the log.
- Fighters drawn into the fight by another player are now recognized by every client and counted in the results.
- A death no longer appears several times in the kill feed, and kills are no longer credited to "the elements"
  by mistake.
- With friendly fire disabled, healers and support creatures can heal and buff their allies again.
- With the mod on the server, fighters of a player who has left no longer remain frozen.
- A released intruder gets its world state back: night spawn, raid membership, loot, fleeing and sleeping.
- The default camera key is now F3; an F9 saved in the config is changed to F3.
- Esc in a text field of the mod no longer opens the game menu. The `[` `]` `-` `=` keys work only while an arena
  is deployed and no longer fire while a map pin name or a build search is being typed.
- The camera no longer turns while the mouse moves toward a button; looking around requires the right mouse
  button.
- Closing the game menu no longer resets the arena's slow motion.
- Over water, the flyer ceiling is measured from the water surface rather than the sea floor.
- Loading a scenario keeps the character, camera and overlay settings.
- The star look correction applies to arena fighters only.

Исправления:

- Состав, сценарии и правки атак, сопротивлений и слабых мест в профилях снова сохраняются, а гости получают
  правила арены хозяина.
- Профиль, сохранённый в 1.3.0, больше не сбрасывает существу сопротивления на «обычные».
- Щелчок больше не попадает в соседнюю кнопку, если список в этот момент изменился, например когда боец погиб
  или добавлена группа.
- Кнопки, которые меняют окно, больше не вызывают ошибок в логе.
- Бойцы, вовлечённые в бой другим игроком, теперь распознаются всеми клиентами и учитываются в итогах.
- Гибель бойца больше не появляется в ленте убийств по нескольку раз, а убийства не записываются на «стихию»
  по ошибке.
- При выключенном дружественном огне лекари и поддержка снова лечат и усиливают союзников.
- При моде на сервере бойцы ушедшего игрока больше не остаются замороженными.
- Отпущенному чужаку возвращается его состояние в мире: ночное появление, участие в набеге, добыча, бегство
  и сон.
- Клавиша камеры по умолчанию теперь F3; сохранённая в настройках F9 заменяется на F3.
- Esc в поле ввода мода больше не открывает меню игры. Клавиши `[` `]` `-` `=` работают только при развёрнутой
  арене и не срабатывают, пока набирается подпись метки на карте или поиск постройки.
- Камера больше не поворачивается, пока мышь движется к кнопке; обзор требует зажатой правой кнопки.
- Закрытие меню игры больше не сбрасывает замедление времени арены.
- Над водой потолок для летающих отсчитывается от поверхности воды, а не от дна.
- Загрузка сценария не меняет настройки персонажа, камеры и оверлея.
- Поправка облика по звёздам применяется только к бойцам арены.

## 1.3.0

First release; the README describes the full feature set. The interface is available in English and Russian.

Первый выпуск; возможности описаны в README. Интерфейс доступен на английском и русском.
