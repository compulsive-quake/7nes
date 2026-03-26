# Controllers (Joypads)

## Overview

NES supports two standard controllers mapped to $4016 (player 1) and $4017 (player 2).

## Protocol

Controllers use a **strobe/shift register** protocol:

1. Write `$01` to $4016 → strobe on (continuously reload button states)
2. Write `$00` to $4016 → strobe off (latch current state)
3. Read $4016 eight times → returns one button per read (bit 0)
4. After all 8 buttons read, subsequent reads return `1`

## Button Order

| Read # | Button |
|--------|--------|
| 1 | A |
| 2 | B |
| 3 | Select |
| 4 | Start |
| 5 | Up |
| 6 | Down |
| 7 | Left |
| 8 | Right |

## Strobe Behavior

- **Strobe on** (bit 0 of write = 1): Every read returns button A status
- **Strobe off** (bit 0 of write = 0): Each read advances to next button

## Implementation

```
Joypad {
    strobe: bool
    button_index: u8 (0-7, then 8+ returns 1)
    button_status: 8-bit bitfield
}

Write: if bit 0 set, strobe=true, reset index to 0
       if bit 0 clear, strobe=false
Read:  if index > 7, return 1
       result = (status >> index) & 1
       if !strobe: index++
       return result
```

Both controllers share the strobe write signal ($4016 write affects both).
