# AutoLogout

![Screenshot of AutoLogout timer window on the Windows 11 desktop](demo.jpg)

Install this utility on a Windows profile to create a simple, time-limited session.

A timer appears on the bottom right of the Desktop, and a sound will play when 10 minutes remain. If a timer would stretch on beyond bedtime, it is shortened.

In future versions, there will be a parental dashboard to control the time given each day. But for now the time limit is hardcoded to 2 hours, with bedtime from 9pm till 8am.

## Limitations

- This version is intended for users that aren't able to log in without supervision. For example, if they aren't given the password for their account.
  - This account also shouldn't be an Administrator, for multiple reasons.
  - Fow now, the timer resets each time the user logs in too.
- The entire program runs within userspace, meaning it is possible for a technical user to find and kill the process, or prevent it from starting automatically.
  - You can protect files using permissions in Windows, however.
- Sleeping the computer pauses the timer in an unintended way.
- While the timer is paused, the computer will not shut down past bedtime.
- The pause feature is not intended for multiple monitor setups.
- Untested behaviour if the account is locked or switched when the timer runs out.

## Instalation

Download the latest zip file in [releases](releases) and follow the instructions from there.
