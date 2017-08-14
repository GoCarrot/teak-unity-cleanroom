require "rake/clean"
CLEAN.include "**/.DS_Store"

desc "Build Unity package"
task :default

UNITY_HOME = "#{ENV['UNITY_HOME'] || '/Applications/Unity'}"
RVM_VARS = %w(GEM_HOME IRBRC MY_RUBY_HOME GEM_PATH)
PROJECT_PATH = Dir.pwd

#
# Helper methods
#
def xcodebuild(*args)
  sh "xcodebuild #{args.join(' ')}"
end

def unity(*args)
  # Run Unity.
  sh "#{UNITY_HOME}/Unity.app/Contents/MacOS/Unity -logFile #{PROJECT_PATH}/unity.log -quit -batchmode -nographics -projectPath #{PROJECT_PATH} #{args.join(' ')}"
end

def unity?
  File.exist? "#{UNITY_HOME}/Unity.app/Contents/MacOS/Unity"
end

#
# Tasks
#
task :clean do
  sh "git clean -fdx"
end

namespace :package do
  task :download do
    Rake::Task["clean"].invoke
    sh "curl -o Teak.unitypackage https://s3.amazonaws.com/teak-build-artifacts/unity/Teak.unitypackage"
  end

  task :copy do
    Rake::Task["clean"].invoke
    cp '../teak-unity/Teak.unitypackage', 'Teak.unitypackage'
  end

  task :import do
    File.open('Assets/smcs.rsp', 'w') do |f|
      f.puts "-define:TEAK_NOT_AVAILABLE"
    end

    unity "-importPackage Teak.unitypackage"

    File.delete('Assets/smcs.rsp')
  end
end

namespace :build do
  task :android do
    unity "-executeMethod BuildPlayer.Android"
  end

  task :ios do
    Rake::Task["ios:build"].invoke
    Rake::Task["ios:postprocess"].invoke
    Rake::Task["ios:xcodebuild"].invoke

    
  end
end

namespace :ios do
  task :build do
    unity "-executeMethod BuildPlayer.iOS"
  end

  task :postprocess do
    cp 'iOSResources/Unity-iPhone/Unity-iPhone.entitlements', 'iOSBuild/Unity-iPhone/Unity-iPhone.entitlements'
    sh "ruby iOSResources/AddEntitlements.rb Unity-iPhone"
  end

  task :xcodebuild do
    Dir.chdir('iOSBuild') do
      xcodebuild "-project Unity-iPhone.xcodeproj -scheme Unity-iPhone -sdk iphoneos -configuration Release clean archive -archivePath build/archive DEVELOPMENT_TEAM=7FLZTACJ82"
    end
  end

  task :export do
    old = {}
    RVM_VARS.each do |key|
      old[key] = ENV[key]
    end
    old['PATH'] = ENV['PATH']

    begin
      %w(GEM_HOME IRBRC MY_RUBY_HOME GEM_PATH).each do |var|
        ENV.delete(var)
      end
      ENV['PATH'] = ENV['PATH'].split(':').reject { |elem| elem =~ /\.rvm/ }.join(':')
      xcodebuild '-exportArchive -archivePath iOSBuild/build/archive.xcarchive -exportOptionsPlist iOSResources/exportOptions.plist -exportPath iOSBuild/build/'
    ensure
      old.each do |key, value|
        ENV[key] = value
      end
    end

    cp 'iOSBuild/build/Unity-iPhone.ipa', 'teak-unity-cleanroom.ipa'
  end
end

namespace :install do
  task :ios do
    sh "ideviceinstaller -i teak-unity-cleanroom.ipa"
  end

  task :android do
    devicelist = %x[AndroidResources/devicelist].split(',').collect{ |x| x.chomp }
    devicelist.each do |device|
      adb = lambda { |*args| sh "adb -s #{device} #{args.join(' ')}" }

      adb.call "uninstall com.teakio.pushtest"
      adb.call "install teak-unity-cleanroom.apk"
      adb.call "shell am start -W -a android.intent.action.VIEW -d https://teakangrybots.jckpt.me/ESW-__uzW com.teakio.pushtest"
    end
  end
end
