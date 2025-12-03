# TALETOY

Developed for the November [Game Creators Club](https://game-creators-club.itch.io/) at Lusofona University, with theme "What If".

This is a toy application, not exactly a game.
The concept is that this is a grid-based 'life sandbox' where each tile contains a symbolic life event ('baby', 'coin', 'weapon', 'stranger', 'job', 'accident', etc).
The player moves one tile per year. When next to an icon, they can interact with it in one of several ways - each representing a 'What If' moment. The world itself is a compressed metaphor of a whole life.

When they die (death probability grows with age), all choices are collected into a story skeleton.
This becomes the structured input for an LLM, which returns a stylized short story about 'this life'.

## Todo

* Finetune a small model
* Credits

## Tech stuff regarding the LLM

I'm using llama.cpp, so here's some information regarding how to compile

- Make libcurl available
  - Install through vcpkg (.\vcpkg.exe install curl:x64-windows)
  - When running the build project scripts of llama.cpp, add "-DCMAKE_TOOLCHAIN_FILE=C:/path/to/vcpkg/scripts/buildsystems/vcpkg.cmake"
- Generate SLN and build:
  ```
  git clone https://github.com/ggml-org/llama.cpp
  cd llama.cpp
  mkdir build
  cd build
  cmake .. -G "Visual Studio 17 2022" -A x64 -DCMAKE_TOOLCHAIN_FILE="C:\projects\Other\vcpkg\scripts\buildsystems\vcpkg.cmake"
  cmake --build . --config Release 
  ```
- [Find a model](https://huggingface.co/models?search=gguf) - I selected [Llama-3.1-8B-Instruct](https://huggingface.co/meta-llama/Llama-3.1-8B-Instruct)
- Now, now we can try the model and see if everything is working.
  ```
  cd bin
  cd Release
  llama-cli.exe -m c:\projects\Other\models\Meta-Llama-3.1-8B-Instruct-Q4_K_L.gguf -p "Write a short story about a farmer who restores an old barn, with no more than 4 paragraphs."
  ```
- Now we need to build a DLL wrapper. The file is available on WrapperDLL/llm_wrapper.cpp. The CMakeLists.txt file is just a sample, you need to add this to the existing file on llama.cpp:
  ```
  add_library(llm_wrapper SHARED custom/llm_wrapper.cpp)
  target_link_libraries(llm_wrapper PRIVATE llama)
  target_include_directories(llm_wrapper PRIVATE .)
  target_include_directories(llm_wrapper PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/include)
  ```
- I'm not going to distribute the model here, and I'll add instructions on the itch.io page of the game, since it's a 5Gb download!

## Art

- Font [Infinite Grateful](https://chequered.ink/product/infinitely-grateful/) by [Checkered Ink](https://chequered.ink/), purchased and under the [Checkered Ink License](https://chequered.ink/wp-content/uploads/2025/01/License-Agreement-All-Fonts-Pack.pdf)
- [1-bit Pack](https://kenney.nl/assets/1-bit-pack) by [Kenney](https://kenney.nl/), [CC0] license.
- Photo [Photography of Book Page](https://www.pexels.com/photo/photography-of-book-page-1029141/) by [Nitin Arya](https://www.pexels.com/@nitin-arya-386173/), free to use
- Everything else done by [Diogo de Andrade], [CC0] license.

## Model

- Model [SmolLM2-135M-Instruct-Q6_K_L](https://huggingface.co/bartowski/SmolLM2-135M-Instruct-GGUF), [Apache2] license.
- Quality is terrible for the game's purposes
  - To be expected, the model is too small (140Mb, 135M params)- might work with fine-tunning (work for the future)
- To use another model, just find one (I used mainly [Meta-Llama-3.1-8B-Instruct-Q4_K_L](https://huggingface.co/bartowski/Meta-Llama-3.1-8B-Instruct-GGUF) (5Gb, 8B params) for my tests)
  - Download the GGUF file and place it in the Taletoy_Data/StreamingAssets/Models directory.
  - You can now select the model from the options on the game
  - If you want to use it in Unity directly, just copy the GGUF to the StreamingAssets/Model directory and change the modelName on the StoryManager prefab.

## Code

- Uses llama-cpp, [MIT] license.
- Some code was adapted/refactored from [Okapi Kit], [MIT] license.
- Uses [Unity Common], [MIT] license.
- [NaughtyAttributes] by Denis Rizov available through the [MIT] license: https://github.com/dbrizov/NaughtyAttributes.git#upm
- All remaining game source code by Diogo de Andrade is licensed under the [MIT] license.

## Metadata

- Autor: [Diogo de Andrade]

[Diogo de Andrade]:https://github.com/DiogoDeAndrade
[CC0]:https://creativecommons.org/publicdomain/zero/1.0/
[CC-BY 3.0]:https://creativecommons.org/licenses/by/3.0/
[CC-BY-NC 3.0]:https://creativecommons.org/licenses/by-nc/3.0/
[CC-BY-SA 4.0]:http://creativecommons.org/licenses/by-sa/4.0/
[CC-BY 4.0]:https://creativecommons.org/licenses/by/4.0/
[CC-BY-NC 4.0]:https://creativecommons.org/licenses/by-nc/4.0/
[OkapiKit]:https://github.com/VideojogosLusofona/OkapiKit
[Unity Common]:https://github.com/DiogoDeAndrade/UnityCommon
[Global Game Jam'25]:https://globalgamejam.org/
[Wilson Almeida]:https://wilson.itch.io/
[Fab Standard License]:https://www.fab.com/eula
[SIL-OFL]:https://openfontlicense.org/open-font-license-official-text/
[MIT]:LICENSE
[Apache2]:https://www.apache.org/licenses/LICENSE-2.0
