// Binary RGB float32 stdin/stdout bridge to the unmodified Academy CTL interpreter.
// argv: peak-nits, pinned-reference-directory. Input is linear AP1 for testing.
#include <CtlSimdInterpreter.h>
#include <CtlFunctionCall.h>
#include <iostream>
#include <sstream>
#include <vector>
#include <stdexcept>
#include <algorithm>

int main(int argc, char** argv)
{
    try
    {
        if(argc!=3)throw std::runtime_error("Expected peak-nits and reference-directory");
        const double peak=std::stod(argv[1]);
        Ctl::SimdInterpreter interpreter;
        interpreter.setModulePaths({argv[2]});
        std::ostringstream source;
        source << "import \"Lib.Academy.Utilities\";\n"
                  "import \"Lib.Academy.Tonescale\";\n"
                  "import \"Lib.Academy.OutputTransform\";\n"
                  "const Chromaticities LIMIT = "
               << (peak==100?"{{.64,.33},{.30,.60},{.15,.06},{.3127,.3290}};\n":"{{.708,.292},{.170,.797},{.131,.046},{.3127,.3290}};\n")
               << "const ODTParams P = init_ODTParams(" << peak << ".0,LIMIT);\n"
                  "void main(input varying float r, input varying float g, input varying float b,"
                  "output varying float ro, output varying float go, output varying float bo) {"
                  "float ap1[3]={r,g,b}; float ap0[3]=mult_f3_f33(ap1,AP1_TO_AP0);"
                  "float rgb[3]=outputTransform_fwd(ap0,P);ro=rgb[0];go=rgb[1];bo=rgb[2];}\n";
        interpreter.loadModule("Aces2Check","Aces2Check.ctl",source.str());
        auto fn=interpreter.newFunctionCall("main");
        std::vector<float> input;
        float v;
        while(std::cin.read(reinterpret_cast<char*>(&v),sizeof(v)))input.push_back(v);
        if(input.size()%3)throw std::runtime_error("Incomplete RGB sample");
        // Pass float samples directly to the interpreter, without image encoding.
        for(size_t i=0;i<input.size();)
        {
            const size_t count=std::min((input.size()-i)/3,size_t(interpreter.maxSamples()));
            for(int c=0;c<3;c++)for(size_t j=0;j<count;j++)reinterpret_cast<float*>(fn->inputArg(c)->data())[j]=input[i+j*3+c];
            fn->callFunction(count);
            for(size_t j=0;j<count;j++)for(int c=0;c<3;c++)
            {
                float out=reinterpret_cast<float*>(fn->outputArg(c)->data())[j];
                std::cout.write(reinterpret_cast<const char*>(&out),sizeof(out));
            }
            i+=count*3;
        }
    }
    catch(const std::exception& e){std::cerr<<e.what()<<std::endl;return 1;}
}
